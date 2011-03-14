/***************************************
***   Static data management script  ***
***************************************/

-- This script will manage the static data from
-- your Team Database project for <TABLENAME>.

PRINT 'Updating static data table <TABLENAME>'

-- Turn off affected rows being returned
SET NOCOUNT ON

-- Change this to 1 to delete missing records in the target
-- WARNING: Setting this to 1 can cause damage to your database
-- and cause failed deployment if there are any rows referencing
-- a record which has been deleted.
DECLARE @DeleteMissingRecords BIT
SET @DeleteMissingRecords = 0

-- 1: Define table variable
{0}

-- 2: Populate the table variable with data
-- This is where you manage your data in source control. You
-- can add and modify entries, but because of potential foreign
-- key contraint violations this script will not delete any
-- removed entries. If you remove an entry then it will no longer
-- be added to new databases based on your schema, but the entry
-- will not be deleted from databases in which the value already exists.
{1}

-- 3: Insert any new items into the table from the table variable
{2}

-- 4: Update any modified values with the values from the table variable
{3}

-- 5: Delete any missing records from the target
IF @DeleteMissingRecords = 1
BEGIN
{4}
END

PRINT 'Finished updating static data table <TABLENAME>'

-- Note: If you are not using the new GDR version of DBPro
-- then remove this go command.
GO