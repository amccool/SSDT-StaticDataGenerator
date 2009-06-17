using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using StaticGeneratorCommon;
using System.Data.SqlClient;
using System.IO;
using System.Collections;

namespace StaticGenerator
{
    public partial class frmGenerate : Form
    {
        public frmGenerate()
        {
            InitializeComponent();
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            fbdDropFolder.ShowDialog();
            if (string.IsNullOrEmpty(fbdDropFolder.SelectedPath) == false)
            {
                txtFolder.Text = fbdDropFolder.SelectedPath;
            }
        }

        private void frmGenerate_Load(object sender, EventArgs e)
        {
            LoadTableList();
        }

        private void LoadTableList()
        {
            try
            {
                string strConnectionString = ConfigurationManager.ConnectionStrings["default"].ToString();
                Globals.ConnectionString = strConnectionString;

                DataTable dtTableList = new DataTable("Tables");
                SqlCommand cdTableList = Globals.Connection.CreateCommand();
                cdTableList.CommandText = "select table_name as Name from INFORMATION_SCHEMA.Tables where TABLE_TYPE = 'BASE TABLE'";
                dtTableList.Load(cdTableList.ExecuteReader(CommandBehavior.CloseConnection));

                foreach (DataRow drTableName in dtTableList.Rows)
                {
                    clbTables.Items.Add(drTableName[0].ToString());
                }

                clbTables.Sorted = true;
            }
            catch (SqlException ex)
            {
                MessageBox.Show("An error occured while attempting to open the table list. Have you specified a connection string in the settings file?" +
                    Environment.NewLine + Environment.NewLine + "Error text: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occured while attempting to open the table list." +
                    Environment.NewLine + Environment.NewLine + "Error text: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnGenerateScripts_Click(object sender, EventArgs e)
        {
            // Make sure they have selected a folder
            if (string.IsNullOrEmpty(txtFolder.Text) == true)
            {
                MessageBox.Show("Please select a folder to drop the generated script(s) into", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            // Make sure they have selected at least one table
            if (clbTables.CheckedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one table to generate scripts for", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            // Make sure the template exists
            if (File.Exists("DefaultTemplate.sql") == false)
            {
                MessageBox.Show("Template file DefaultTemplate.sql not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            // Load the template
            StreamReader srTemplate = new StreamReader("DefaultTemplate.sql");
            string strTemplate = srTemplate.ReadToEnd();
            srTemplate.Close();

            foreach (string strTableName in clbTables.CheckedItems)
            {
                // Replace the tablename placeholder in the template
                string strNewTemplate = strTemplate.Replace("<TABLENAME>", strTableName);

                // Create the file
                StreamWriter swOutFile = new StreamWriter(Path.Combine(txtFolder.Text, strTableName) + ".staticdata.sql", false);
                swOutFile.Write(Globals.CreateStaticDataManager(strTableName, strNewTemplate));
                swOutFile.Close();
            }

            if (chkCreateIndex.Checked == true)
            {
                // Create index file
                StreamWriter swIndex = new StreamWriter(Path.Combine(txtFolder.Text, "index.txt"), false);
                foreach (string strTableName in clbTables.CheckedItems)
                {
                    swIndex.WriteLine(":r .\\StaticData\\" + strTableName + ".staticdata.sql");
                }
                swIndex.Close();
            }

            MessageBox.Show("Done!!", "Static Data Script Generator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}