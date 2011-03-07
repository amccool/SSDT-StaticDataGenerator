using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Collections;
using System.Linq;

namespace StaticGeneratorCommon
{
    public static class Globals
    {
        #region Class Data

        private static string _strConnectionString;
        private static SqlConnection _cnConnection;
        private static string[] _strTypesWithPrecision = {"varchar", "nvarchar", "decimal", "char", "nchar"};

        #endregion

        #region Properties

        /// <summary>
        /// The connection string to use for processing
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                return _strConnectionString;
            }
            set
            {
                _strConnectionString = value;
            }
        }

        /// <summary>
        /// Self opening and managing connection object
        /// </summary>
        public static SqlConnection Connection
        {
            get
            {
                if (_strConnectionString == null || string.IsNullOrEmpty(_strConnectionString))
                {
                    // They have not set the connection string yet
                    throw new Exception("Connection string has not yet been set");
                }
                if (_cnConnection == null)
                {
                    // Instantiate the connection
                    _cnConnection = new SqlConnection(_strConnectionString);
                }
                if (_cnConnection.State == System.Data.ConnectionState.Closed)
                {
                    // Open the connection
                    _cnConnection.Open();
                }
                // Return the reference
                return _cnConnection;
            }
            set
            {
                // In case they want to just pass in an already
                // open SQLConnection object
                _cnConnection = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Generates the Static Data Management (SDM) script using the
        /// provided template.
        /// </summary>
        /// <param name="pstrTableName">The table to generate a script for</param>
        /// <param name="pstrTemplate">
        /// Template which has {0} - {3} format placeholders which will be
        /// populated from this method
        /// </param>
        /// <returns></returns>
        public static string CreateStaticDataManager(string pstrTableName, string pstrTemplate)
        {
            // Create a SQLCommand to pull back the required data
            SqlCommand cdTableInfo = new SqlCommand("select * from " + pstrTableName, Connection);

            // Get data
            SqlDataAdapter daTableInfo = new SqlDataAdapter(cdTableInfo);

            try
            {
                DataSet dsTableInfo = new DataSet();
                daTableInfo.Fill(dsTableInfo);

                // Get Schema
                DataTable dtTableSchema = cdTableInfo.ExecuteReader(CommandBehavior.KeyInfo |
                    CommandBehavior.SchemaOnly).GetSchemaTable();

                // Create table definition and column list
                string strTableDef = "DECLARE @tblTempTable TABLE (" + Environment.NewLine;
                ArrayList strColumns = new ArrayList();
                bool blnHasIdentity = false;
                ArrayList strPrimaryKeyColumns = new ArrayList();
                foreach (DataRow drTableField in dtTableSchema.Rows)
                {
                    // Is this the primary key?
                    if ((bool)drTableField[dtTableSchema.Columns["IsKey"]] == true)
                    {
                        strPrimaryKeyColumns.Add(drTableField[dtTableSchema.Columns["ColumnName"]].ToString());
                    }
                    // Is this an identity column?
                    if ((bool)drTableField[dtTableSchema.Columns["IsIdentity"]] == true)
                    {
                        blnHasIdentity = true;
                    }

                    // Add to the column list if this is not a timestamp column
                    if (drTableField[dtTableSchema.Columns["DataTypeName"]].ToString().Equals("timestamp") == false)
                    {
                        strColumns.Add(drTableField[dtTableSchema.Columns["ColumnName"]].ToString());
                    }

                    // Add to the table definition
                    string strNewLine = string.Format("[{0}] {1}", drTableField[dtTableSchema.Columns["ColumnName"]], drTableField[dtTableSchema.Columns["DataTypeName"]].ToString());
                    if (_strTypesWithPrecision.Contains(drTableField[dtTableSchema.Columns["DataTypeName"]].ToString()))
                    {
                        if ((drTableField[dtTableSchema.Columns["DataTypeName"]].ToString().Equals("varchar") || drTableField[dtTableSchema.Columns["DataTypeName"]].ToString().Equals("nvarchar")) &&
                            drTableField[dtTableSchema.Columns["ColumnSize"]].ToString() == "2147483647")
                        {
                            // MAX varchar column
                            strNewLine += "(MAX)";
                        }
                        else
                        {
                            strNewLine += "(" + drTableField[dtTableSchema.Columns["ColumnSize"]].ToString() + ")";
                        }
                    }
                    strTableDef += strNewLine + "," + Environment.NewLine;
                }
                // Remove trailing comma and add closing bracket
                strTableDef = strTableDef.Substring(0, strTableDef.Length - 3) + Environment.NewLine + ")";

                // Die if we do not have a primary key
                if (strPrimaryKeyColumns.Count == 0)
                {
                    throw new Exception(string.Format("A primary key is required on {0} to synchronize changes to the table!", pstrTableName));
                }

                // Create insert stub
                string strInsert = "INSERT INTO @tblTempTable (";
                foreach (DataRow drTableField in dtTableSchema.Rows)
                {
                    strInsert += string.Format("[{0}], ", drTableField[dtTableSchema.Columns["ColumnName"]]);
                }
                // Remove trailing comma and add closing bracket
                strInsert = strInsert.Substring(0, strInsert.Length - 2) + ")";

                string strTmpInsert = "";
                StringBuilder sbTmpInsert = new StringBuilder();

                // Add table data
                foreach (DataRow drResult in dsTableInfo.Tables[0].Rows)
                {
                    // Add the opening bracket
                    sbTmpInsert.Append(strInsert + " VALUES (");
                    for (int i = 0; i < dsTableInfo.Tables[0].Columns.Count; i++)
                    {
                        // Add the values to the insert statement
                        string strValue = drResult[i].ToString().Replace("'", "''");
                        if (drResult[i].Equals(DBNull.Value) || strValue.Equals("System.Byte[]") || strValue == "system.dbnull")
                        {
                            // If the value is null then use NULL
                            strValue = "NULL";
                        }
                        else
                        {
                            // Always add quotes around the value, as SQL Server can handle this
                            strValue = "'" + strValue + "'";
                        }
                        if (i == dsTableInfo.Tables[0].Columns.Count - 1)
                        {
                            sbTmpInsert.Append(strValue + ")" + Environment.NewLine);
                        }
                        else
                        {
                            sbTmpInsert.Append(strValue + ", ");
                        }
                    }
                }
                strTmpInsert = sbTmpInsert.ToString();

                // Create live insert code
                string strLiveInsert = "INSERT INTO " + pstrTableName + " (" + FlattenColumnList(strColumns, null) + ")" + Environment.NewLine +
                     "SELECT " + FlattenColumnList(strColumns, "tmp") + Environment.NewLine +
                     "FROM @tblTempTable tmp" + Environment.NewLine + "LEFT JOIN " + pstrTableName + " tbl ON ";
                foreach (string strKey in strPrimaryKeyColumns)
                {
                    strLiveInsert += "tbl.[" + strKey + "] = tmp.[" + strKey + "] AND ";
                }
                strLiveInsert = strLiveInsert.Substring(0, strLiveInsert.Length - 5) + Environment.NewLine + "WHERE ";
                foreach (string strKey in strPrimaryKeyColumns)
                {
                    strLiveInsert += "tbl.[" + strKey + "] IS NULL AND ";
                }
                strLiveInsert = strLiveInsert.Substring(0, strLiveInsert.Length - 5);

                // Add IDENTITY INSERT statements if this table has an identity column
                if (blnHasIdentity == true)
                {
                    strLiveInsert = "SET IDENTITY_INSERT " + pstrTableName + " ON" + Environment.NewLine +
                        strLiveInsert + Environment.NewLine +
                        "SET IDENTITY_INSERT " + pstrTableName + " OFF";
                }

                // Build sync script
                string strUpdate = "UPDATE LiveTable SET" + Environment.NewLine;
                foreach (DataRow myField in dtTableSchema.Rows)
                {
                    // Add the column if this is not a timestamp column or the primary key
                    if (myField[dtTableSchema.Columns["DataTypeName"]].ToString().Equals("timestamp") == false &&
                        strPrimaryKeyColumns.Contains(myField[dtTableSchema.Columns["ColumnName"]].ToString()) == false)
                    {
                        string strColumnName = myField[dtTableSchema.Columns["ColumnName"]].ToString();
                        strUpdate += "LiveTable.[" + strColumnName + "] = tmp.[" + strColumnName + "]," + Environment.NewLine;
                    }
                }
                // Trim trailing comma and add the rest of the script
                strUpdate = strUpdate.Substring(0, strUpdate.Length - 3) + Environment.NewLine +
                    "FROM " + pstrTableName + " LiveTable " + Environment.NewLine + "INNER JOIN @tblTempTable tmp ON ";
                string strJoinClause = "";
                foreach (string strKey in strPrimaryKeyColumns)
                {
                    strJoinClause += "LiveTable.[" + strKey + "] = tmp.[" + strKey + "] AND ";
                }
                strJoinClause = strJoinClause.Substring(0, strJoinClause.Length - 5);
                strUpdate += strJoinClause;

                // Build delete script
                string strNullJoin = "";
                foreach (string strKey in strPrimaryKeyColumns)
                {
                    strNullJoin += "tmp.[" + strKey + "] IS NULL AND ";
                }
                strNullJoin = strNullJoin.Substring(0, strNullJoin.Length - 5);
                string strDelete = string.Format("\tDELETE FROM {0} FROM {0} LiveTable\n\tLEFT JOIN @tblTempTable tmp ON {1}\n\tWHERE {2}", pstrTableName, strJoinClause, strNullJoin);

                // Format the provided template with our output
                string strOutputScript = string.Format(pstrTemplate, strTableDef, strTmpInsert, strLiveInsert, strUpdate, strDelete);
                return strOutputScript;
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                cdTableInfo.Dispose();
                daTableInfo.Dispose();
                Connection.Close();
            }

        }

        /// <summary>
        /// Formats the column list array in the format "t.col1, t.col2"
        /// </summary>
        /// <param name="strColumns">Arraylist of column names</param>
        /// <param name="strTablePrefix">If required, the table alias to prefix columns with</param>
        /// <returns></returns>
        private static string FlattenColumnList(ArrayList strColumns, string strTablePrefix)
        {
            string strList = "";
            foreach (string strColumn in strColumns)
            {
                if (string.IsNullOrEmpty(strTablePrefix))
                {
                    strList += string.Format("[{0}], ", strColumn);
                }
                else
                {
                    strList += string.Format("{0}.[{1}], ", strTablePrefix, strColumn);
                }
            }
            return strList.Substring(0, strList.Length - 2);
        }

        #endregion
    }
}
