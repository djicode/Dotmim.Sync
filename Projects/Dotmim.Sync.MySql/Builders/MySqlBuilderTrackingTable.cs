﻿using Dotmim.Sync.Builders;
using System;
using System.Text;
using Dotmim.Sync.Data;
using System.Data.Common;
using Dotmim.Sync.Log;
using System.Data;
using MySql.Data.MySqlClient;
using Dotmim.Sync.Filter;
using System.Linq;
using Dotmim.Sync.MySql.Builders;
using System.Diagnostics;
using System.Collections.Generic;

namespace Dotmim.Sync.MySql
{
    public class MySqlBuilderTrackingTable : IDbBuilderTrackingTableHelper
    {
        private ObjectNameParser tableName;
        private ObjectNameParser trackingName;
        private DmTable tableDescription;
        private MySqlConnection connection;
        private MySqlTransaction transaction;
        public IList<FilterClause2> Filters { get; set; }
        private MySqlDbMetadata mySqlDbMetadata;


        public MySqlBuilderTrackingTable(DmTable tableDescription, DbConnection connection, DbTransaction transaction = null)
        {
            this.connection = connection as MySqlConnection;
            this.transaction = transaction as MySqlTransaction;
            this.tableDescription = tableDescription;
            (this.tableName, this.trackingName) = MySqlBuilder.GetParsers(this.tableDescription);
            this.mySqlDbMetadata = new MySqlDbMetadata();
        }


        public void CreateIndex()
        {


        }

        private string CreateIndexCommandText()
        {
            StringBuilder stringBuilder = new StringBuilder();

            return stringBuilder.ToString();
        }

        public string CreateIndexScriptText()
        {
            string str = string.Concat("Create index on Tracking Table ", trackingName.FullQuotedString);
            return "";
        }

        public void CreatePk()
        {
            bool alreadyOpened = this.connection.State == ConnectionState.Open;
            try
            {
                using (var command = new MySqlCommand())
                {
                    if (!alreadyOpened)
                        this.connection.Open();

                    if (transaction != null)
                        command.Transaction = transaction;

                    command.CommandText = this.CreatePkCommandText();
                    command.Connection = this.connection;

                    // Sometimes we could have an empty string if pk is created during table creation
                    if (!string.IsNullOrEmpty(command.CommandText))
                        command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during CreateIndex : {ex}");
                throw;
            }
            finally
            {
                if (!alreadyOpened && this.connection.State != ConnectionState.Closed)
                    this.connection.Close();
            }
        }
        public string CreatePkScriptText()
        {
            string str = string.Concat("No need to Create Primary Key on Tracking Table since it's done during table creation ", trackingName.FullQuotedString);
            return "";
        }

        public string CreatePkCommandText()
        {
            return "";
        }

        public void CreateTable()
        {
            bool alreadyOpened = this.connection.State == ConnectionState.Open;

            try
            {
                using (var command = new MySqlCommand())
                {
                    if (!alreadyOpened)
                        this.connection.Open();

                    if (this.transaction != null)
                        command.Transaction = this.transaction;

                    command.CommandText = this.CreateTableCommandText();
                    command.Connection = this.connection;
                    command.ExecuteNonQuery();

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during CreateIndex : {ex}");
                throw;

            }
            finally
            {
                if (!alreadyOpened && this.connection.State != ConnectionState.Closed)
                    this.connection.Close();

            }


        }

        public string CreateTableScriptText()
        {
            string str = string.Concat("Create Tracking Table ", trackingName.FullQuotedString);
            return MySqlBuilder.WrapScriptTextWithComments(this.CreateTableCommandText(), str);
        }

        public string CreateTableCommandText()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"CREATE TABLE {trackingName.FullQuotedString} (");

            List<DmColumn> addedColumns = new List<DmColumn>();

            // Adding the primary key
            foreach (DmColumn pkColumn in this.tableDescription.PrimaryKey.Columns)
            {
                var quotedColumnName = new ObjectNameParser(pkColumn.ColumnName, "`", "`").FullQuotedString;

                var columnTypeString = this.mySqlDbMetadata.TryGetOwnerDbTypeString(pkColumn.OriginalDbType, pkColumn.DbType, false, false, pkColumn.MaxLength, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
                var unQuotedColumnType = new ObjectNameParser(columnTypeString, "`", "`").FullUnquotedString;
                var columnPrecisionString = this.mySqlDbMetadata.TryGetOwnerDbTypePrecision(pkColumn.OriginalDbType, pkColumn.DbType, false, false, pkColumn.MaxLength, pkColumn.Precision, pkColumn.Scale, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
                var columnType = $"{unQuotedColumnType} {columnPrecisionString}";

                stringBuilder.AppendLine($"{quotedColumnName} {columnType} NOT NULL, ");

                addedColumns.Add(pkColumn);
            }

            // adding the tracking columns
            stringBuilder.AppendLine($"`create_scope_id` VARCHAR(36) NULL, ");
            stringBuilder.AppendLine($"`update_scope_id` VARCHAR(36) NULL, ");
            stringBuilder.AppendLine($"`create_timestamp` BIGINT NULL, ");
            stringBuilder.AppendLine($"`update_timestamp` BIGINT NULL, ");
            stringBuilder.AppendLine($"`timestamp` BIGINT NULL, ");
            stringBuilder.AppendLine($"`sync_row_is_tombstone` BIT NOT NULL default 0 , ");
            stringBuilder.AppendLine($"`last_change_datetime` DATETIME NULL, ");

            //-----------------------------------------------------------
            // Adding the foreign keys
            //-----------------------------------------------------------
            foreach (var pr in this.tableDescription.ChildRelations)
            {
                // get the parent columns to have the correct name of the column (if we have mulitple columns who is bind to same child table)
                // ie : AddressBillId and AddressInvoiceId
                foreach (var c in pr.ParentColumns)
                {
                    // dont add doublons
                    if (addedColumns.Any(col => col.ColumnName.ToLowerInvariant() == c.ColumnName.ToLowerInvariant()))
                        continue;

                    var quotedColumnName = new ObjectNameParser(c.ColumnName, "`", "`").FullQuotedString;

                    var columnTypeString = this.mySqlDbMetadata.TryGetOwnerDbTypeString(c.OriginalDbType, c.DbType, c.IsUnsigned, c.IsUnicode, c.MaxLength, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
                    var unQuotedColumnType = new ObjectNameParser(columnTypeString, "`", "`").FullUnquotedString;
                    var columnPrecisionString = this.mySqlDbMetadata.TryGetOwnerDbTypePrecision(c.OriginalDbType, c.DbType, c.IsUnsigned, c.IsUnicode, c.MaxLength, c.Precision, c.Scale, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
                    var columnType = $"{unQuotedColumnType} {columnPrecisionString}";
                    var nullableColumn = "NULL";

                    stringBuilder.AppendLine($"{quotedColumnName} {columnType} {nullableColumn}, ");

                    addedColumns.Add(c);

                }
            }

            // ---------------------------------------------------------------------
            // Add the filter columns if needed, and if not already added from Pkeys or Fkeys
            // ---------------------------------------------------------------------
            if (this.Filters != null && this.Filters.Count > 0)
            {
                foreach (var filter in this.Filters)
                {
                    // if column is null, we are in a table that need a relation before
                    if (string.IsNullOrEmpty(filter.FilterTable.ColumnName))
                        continue;

                    var columnFilter = this.tableDescription.Columns[filter.FilterTable.ColumnName];

                    if (columnFilter == null)
                        throw new InvalidExpressionException($"Column {filter.FilterTable.ColumnName} does not exist in Table {this.tableDescription.TableName}");

                    if (addedColumns.Any(ac => ac.ColumnName.ToLowerInvariant() == columnFilter.ColumnName.ToLowerInvariant()))
                        continue;

                    var quotedColumnName = new ObjectNameParser(columnFilter.ColumnName, "[", "]").FullQuotedString;

                    var columnTypeString = this.mySqlDbMetadata.TryGetOwnerDbTypeString(columnFilter.OriginalDbType, columnFilter.DbType, columnFilter.IsUnsigned, columnFilter.IsUnicode, columnFilter.MaxLength, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
                    var unQuotedColumnType = new ObjectNameParser(columnTypeString, "[", "]").FullUnquotedString;
                    var columnPrecisionString = this.mySqlDbMetadata.TryGetOwnerDbTypePrecision(columnFilter.OriginalDbType, columnFilter.DbType, columnFilter.IsUnsigned, columnFilter.IsUnicode, columnFilter.MaxLength, columnFilter.Precision, columnFilter.Scale, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
                    var columnType = $"{unQuotedColumnType} {columnPrecisionString}";

                    var nullableColumn = "NULL";

                    stringBuilder.AppendLine($"{quotedColumnName} {columnType} {nullableColumn}, ");

                    addedColumns.Add(columnFilter);
                }
            }

            addedColumns.Clear();

            stringBuilder.Append(" PRIMARY KEY (");
            for (int i = 0; i < this.tableDescription.PrimaryKey.Columns.Length; i++)
            {
                DmColumn pkColumn = this.tableDescription.PrimaryKey.Columns[i];
                var quotedColumnName = new ObjectNameParser(pkColumn.ColumnName, "`", "`").QuotedObjectName;

                stringBuilder.Append(quotedColumnName);

                if (i < this.tableDescription.PrimaryKey.Columns.Length - 1)
                    stringBuilder.Append(", ");
            }
            stringBuilder.Append("))");

            return stringBuilder.ToString();
        }

        public bool NeedToCreateTrackingTable()
        {
            return !MySqlManagementUtils.TableExists(connection, transaction, trackingName.FullUnquotedString);

        }

        public void PopulateFromBaseTable()
        {
            bool alreadyOpened = this.connection.State == ConnectionState.Open;

            try
            {
                using (var command = new MySqlCommand())
                {
                    if (!alreadyOpened)
                        this.connection.Open();

                    if (this.transaction != null)
                        command.Transaction = this.transaction;

                    command.CommandText = this.CreatePopulateFromBaseTableCommandText();
                    command.Connection = this.connection;
                    command.ExecuteNonQuery();

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during CreateIndex : {ex}");
                throw;

            }
            finally
            {
                if (!alreadyOpened && this.connection.State != ConnectionState.Closed)
                    this.connection.Close();

            }

        }

        private string CreatePopulateFromBaseTableCommandText()
        {
            var addedColumns = new List<DmColumn>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Concat("INSERT INTO ", trackingName.FullQuotedString, " ("));
            StringBuilder stringBuilder1 = new StringBuilder();
            StringBuilder stringBuilder2 = new StringBuilder();
            string empty = string.Empty;
            StringBuilder stringBuilderOnClause = new StringBuilder("ON ");
            StringBuilder stringBuilderWhereClause = new StringBuilder("WHERE ");
            string str = string.Empty;
            string baseTable = "`base`";
            string sideTable = "`side`";
            foreach (var pkColumn in this.tableDescription.PrimaryKey.Columns)
            {
                var quotedColumnName = new ObjectNameParser(pkColumn.ColumnName, "`", "`").FullQuotedString;

                stringBuilder1.Append(string.Concat(empty, quotedColumnName));

                stringBuilder2.Append(string.Concat(empty, baseTable, ".", quotedColumnName));

                string[] quotedName = new string[] { str, baseTable, ".", quotedColumnName, " = ", sideTable, ".", quotedColumnName };
                stringBuilderOnClause.Append(string.Concat(quotedName));
                string[] strArrays = new string[] { str, sideTable, ".", quotedColumnName, " IS NULL" };
                stringBuilderWhereClause.Append(string.Concat(strArrays));
                empty = ", ";
                str = " AND ";

                addedColumns.Add(pkColumn);
            }
            StringBuilder stringBuilder5 = new StringBuilder();
            StringBuilder stringBuilder6 = new StringBuilder();

            //-----------------------------------------------------------
            // Adding the foreign keys
            //-----------------------------------------------------------
            foreach (var pr in this.tableDescription.ChildRelations)
            {
                // get the parent columns to have the correct name of the column (if we have mulitple columns who is bind to same child table)
                // ie : AddressBillId and AddressInvoiceId
                foreach (var c in pr.ParentColumns)
                {
                    // dont add doublons
                    if (addedColumns.Any(col => col.ColumnName.ToLowerInvariant() == c.ColumnName.ToLowerInvariant()))
                        continue;

                    var quotedColumnName = new ObjectNameParser(c.ColumnName, "`", "`").FullQuotedString;

                    stringBuilder6.Append(string.Concat(empty, quotedColumnName));
                    stringBuilder5.Append(string.Concat(empty, baseTable, ".", quotedColumnName));

                    addedColumns.Add(c);

                }
            }

            // ---------------------------------------------------------------------
            // Add the filter columns if needed, and if not already added from Pkeys or Fkeys
            // ---------------------------------------------------------------------
            if (this.Filters != null && this.Filters.Count > 0)
            {
                foreach (var filter in this.Filters)
                {
                    // if column is null, we are in a table that need a relation before
                    if (string.IsNullOrEmpty(filter.FilterTable.ColumnName))
                        continue;

                    var columnFilter = this.tableDescription.Columns[filter.FilterTable.ColumnName];

                    if (columnFilter == null)
                        throw new InvalidExpressionException($"Column {filter.FilterTable.ColumnName} does not exist in Table {this.tableDescription.TableName}");

                    if (addedColumns.Any(ac => ac.ColumnName.ToLowerInvariant() == columnFilter.ColumnName.ToLowerInvariant()))
                        continue;

                    var quotedColumnName = new ObjectNameParser(columnFilter.ColumnName, "`", "`").FullQuotedString;

                    stringBuilder6.Append(string.Concat(empty, quotedColumnName));
                    stringBuilder5.Append(string.Concat(empty, baseTable, ".", quotedColumnName));

                    addedColumns.Add(columnFilter);
                }
            }

            //if (Filters != null)
            //    foreach (var filterColumn in this.Filters)
            //    {
            //        var isPk = this.tableDescription.PrimaryKey.Columns.Any(dm => this.tableDescription.IsEqual(dm.ColumnName, filterColumn.ColumnName));
            //        if (isPk)
            //            continue;

            //        var quotedColumnName = new ObjectNameParser(filterColumn.ColumnName, "`", "`").FullQuotedString;

            //        stringBuilder6.Append(string.Concat(empty, quotedColumnName));
            //        stringBuilder5.Append(string.Concat(empty, baseTable, ".", quotedColumnName));
            //    }

            // (list of pkeys)
            stringBuilder.Append(string.Concat(stringBuilder1.ToString(), ", "));

            stringBuilder.Append("`create_scope_id`, ");
            stringBuilder.Append("`update_scope_id`, ");
            stringBuilder.Append("`create_timestamp`, ");
            stringBuilder.Append("`update_timestamp`, ");
            stringBuilder.Append("`timestamp`, "); // timestamp is not a column we update, it's auto
            stringBuilder.Append("`sync_row_is_tombstone` ");
            stringBuilder.AppendLine(string.Concat(stringBuilder6.ToString(), ") "));
            stringBuilder.Append(string.Concat("SELECT ", stringBuilder2.ToString(), ", "));
            stringBuilder.Append("NULL, ");
            stringBuilder.Append("NULL, ");
            stringBuilder.Append($"{MySqlObjectNames.TimestampValue}, ");
            stringBuilder.Append("0, ");
            stringBuilder.Append($"{MySqlObjectNames.TimestampValue}, ");
            stringBuilder.Append("0");
            stringBuilder.AppendLine(string.Concat(stringBuilder5.ToString(), " "));
            string[] localName = new string[] { "FROM ", tableName.FullQuotedString, " ", baseTable, " LEFT OUTER JOIN ", trackingName.FullQuotedString, " ", sideTable, " " };
            stringBuilder.AppendLine(string.Concat(localName));
            stringBuilder.AppendLine(string.Concat(stringBuilderOnClause.ToString(), " "));
            stringBuilder.AppendLine(string.Concat(stringBuilderWhereClause.ToString(), "; \n"));

            var scriptInsertTrackingTableData = stringBuilder.ToString();

            return scriptInsertTrackingTableData;
        }

        public string CreatePopulateFromBaseTableScriptText()
        {
            string str = string.Concat("Populate tracking table ", trackingName.FullQuotedString, " for existing data in table ", tableName.FullQuotedString);
            return MySqlBuilder.WrapScriptTextWithComments(this.CreatePopulateFromBaseTableCommandText(), str);
        }

        public void PopulateNewFilterColumnFromBaseTable(DmColumn filterColumn)
        {
            throw new NotImplementedException();
        }

        public string ScriptPopulateNewFilterColumnFromBaseTable(DmColumn filterColumn)
        {
            throw new NotImplementedException();
        }

        public void AddFilterColumn(DmColumn filterColumn)
        {
            bool alreadyOpened = this.connection.State == ConnectionState.Open;

            try
            {
                using (var command = new MySqlCommand())
                {
                    if (!alreadyOpened)
                        this.connection.Open();

                    if (this.transaction != null)
                        command.Transaction = this.transaction;

                    command.CommandText = this.AddFilterColumnCommandText(filterColumn);
                    command.Connection = this.connection;
                    command.ExecuteNonQuery();

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during CreateIndex : {ex}");
                throw;

            }
            finally
            {
                if (!alreadyOpened && this.connection.State != ConnectionState.Closed)
                    this.connection.Close();

            }

        }

        private string AddFilterColumnCommandText(DmColumn col)
        {
            var quotedColumnName = new ObjectNameParser(col.ColumnName, "`", "`").FullQuotedString;

            var columnTypeString = this.mySqlDbMetadata.TryGetOwnerDbTypeString(col.OriginalDbType, col.DbType, false, false, col.MaxLength, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
            var columnPrecisionString = this.mySqlDbMetadata.TryGetOwnerDbTypePrecision(col.OriginalDbType, col.DbType, false, false, col.MaxLength, col.Precision, col.Scale, this.tableDescription.OriginalProvider, MySqlSyncProvider.ProviderType);
            var columnType = $"{columnTypeString} {columnPrecisionString}";

            return string.Concat("ALTER TABLE ", quotedColumnName, " ADD ", columnType);
        }
        public string ScriptAddFilterColumn(DmColumn filterColumn)
        {
            var quotedColumnName = new ObjectNameParser(filterColumn.ColumnName, "`", "`");

            string str = string.Concat("Add new filter column, ", quotedColumnName.FullUnquotedString, ", to Tracking Table ", trackingName.FullQuotedString);
            return MySqlBuilder.WrapScriptTextWithComments(this.AddFilterColumnCommandText(filterColumn), str);
        }

        public void DropTable()
        {
            var commandText = $"drop table if exists {trackingName.FullQuotedString}";

            bool alreadyOpened = connection.State == ConnectionState.Open;

            try
            {
                if (!alreadyOpened)
                    connection.Open();

                using (var command = new MySqlCommand(commandText, connection))
                {
                    if (transaction != null)
                        command.Transaction = transaction;

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during DropTableCommand : {ex}");
                throw;
            }
            finally
            {
                if (!alreadyOpened && connection.State != ConnectionState.Closed)
                    connection.Close();

            }

        }

        public string DropTableScriptText()
        {
            var commandText = $"drop table if exists {trackingName.FullQuotedString}";

            var str1 = $"Drop table {trackingName.FullQuotedString}";
            return MySqlBuilder.WrapScriptTextWithComments(commandText, str1);
        }

    }
}
