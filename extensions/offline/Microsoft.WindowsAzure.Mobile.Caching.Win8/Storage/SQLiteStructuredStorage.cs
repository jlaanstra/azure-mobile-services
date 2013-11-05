using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SQLiteWinRT;
using Windows.Storage;
using System.Diagnostics.Contracts;

using System.Runtime.InteropServices;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class SQLiteCacheStorage : IStructuredStorage
    {
        private string dbFile;
        private Database db;

        private IDictionary<string, Column> defaultColumns = new Dictionary<string, Column>()
        {
            { "id", new Column("id", ColumnTypeHelper.GetColumnTypeForClrType(typeof(long)), false, null, 0, true) }, // server id
            { "guid", new Column("guid", ColumnTypeHelper.GetColumnTypeForClrType(typeof(Guid)), false, null, 1, true) }, // globally unique
            { "timestamp", new Column("timestamp", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), true, null, 0, true) }, // timestamp of local item
            { "status", new Column("status", ColumnTypeHelper.GetColumnTypeForClrType(typeof(int)), false, null, 0, true) }, // status: unchanged:0 inserted:1 changed:2 deleted:3
        };

        public SQLiteCacheStorage(string name)
        {
            dbFile = string.Format("{0}.sqlite", name);
        }

        public async Task<IDisposable> OpenAsync()
        {
            db = new Database(ApplicationData.Current.LocalFolder, dbFile);

            Debug.WriteLine("Opening database: {0}", this.dbFile);
            await db.OpenAsync(SqliteOpenMode.OpenOrCreateReadWrite);

            return new Disposable(() =>
            {
                if (db != null)
                {
                    db.Dispose();
                    db = null;
                }
            });
        }

        /// <summary>
        /// Gets the stored data.
        /// This method should not alter the database in any way.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="query">The query.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public async Task<JArray> GetStoredData(string tableName, IQueryOptions query)
        {
            Debug.WriteLine("Retrieving data for table {0}, query: {1}", tableName, query.ToString());

            try
            {
                //check if database exists
                if (!await db.DoesTableExistAsync(tableName).ConfigureAwait(false))
                {
                    Debug.WriteLine("Table doesn't exist: {0}", tableName);
                    return new JArray();
                }

                Debug.WriteLine("Table exists: {0}", tableName);
                Debug.WriteLine("Querying table structure for table: {0}", tableName);

                IDictionary<string, Column> columns = await db.GetColumnsForTableAsync(tableName);

                Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", columns.Values));

                SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();
                visitor.VisitQueryOptions(query);

                var sqlStatement = string.Format(visitor.SqlStatement, tableName);

                Debug.WriteLine("Executing sql: {0}", sqlStatement);

                return await db.QueryAsync(sqlStatement, columns).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var result = Database.GetSqliteErrorCode(ex.HResult);
                throw new Exception(string.Format("Error occurred during interaction with the local database: ", result));
            }
        }

        public async Task StoreData(string tableName, JArray data)
        {
            Debug.WriteLine("Storing data for table {0}", tableName);

            if (!data.Any())
            {
                return;
            }

            Exception exception = null;
            // start transaction
            await db.BeginTransaction().ConfigureAwait(false);
            try
            {
                // the actual columns for the item
                IDictionary<string, Column> columns = this.GetColumnsFromItem(data.First as JObject);

                await this.EnsureSchemaForTable(tableName, db, columns).ConfigureAwait(false);

                Debug.WriteLine("Inserting...");

                foreach (JObject item in data)
                {
                    await db.InsertIntoTableAsync(tableName, columns, item).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // rollback if exception
            if (exception != null)
            {
                var result = Database.GetSqliteErrorCode(exception.HResult);
                await db.RollbackTransaction().ConfigureAwait(false);
                throw new Exception(string.Format("Error occurred during interaction with the local database: ", exception));
            }
            else
            {
                await db.CommitTransaction().ConfigureAwait(false);
            }
        }

        public async Task UpdateData(string tableName, JArray data)
        {
            Debug.WriteLine("Update table {0}", tableName);

            if (!data.Any())
            {
                return;
            }

            Exception exception = null;
            await db.BeginTransaction();
            try
            {
                // the actual columns for the item
                IDictionary<string, Column> columns = this.GetColumnsFromItem(data.First as JObject);

                await this.EnsureSchemaForTable(tableName, db, columns).ConfigureAwait(false);

                Debug.WriteLine("Updating...");

                foreach (JObject item in data)
                {
                    await db.UpdateInTableAsync(tableName, columns, item).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // rollback if exception
            if (exception != null)
            {
                var result = Database.GetSqliteErrorCode(exception.HResult);
                await db.RollbackTransaction().ConfigureAwait(false);
                throw new Exception(string.Format("Error occurred during interaction with the local database: ", exception));
            }
            else
            {
                await db.CommitTransaction().ConfigureAwait(false);
            }
        }

        public async Task RemoveStoredData(string tableName, IEnumerable<string> guids)
        {
            Debug.WriteLine(string.Format("Removing data for table {0}, guids: {1}", tableName, string.Join(",", guids)));

            if (!guids.Any())
            {
                return;
            }

            Exception exception = null;
            await db.BeginTransaction().ConfigureAwait(false);
            try
            {
                Debug.WriteLine("Deleting...");

                await db.DeleteFromTableAsync(tableName, guids).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // rollback if exception
            if (exception != null)
            {
                var result = Database.GetSqliteErrorCode(exception.HResult);
                await db.RollbackTransaction().ConfigureAwait(false);
                throw new Exception(string.Format("Error occurred during interaction with the local database: ", exception));
            }
            else
            {
                await db.CommitTransaction().ConfigureAwait(false);
            }
        }

        private IDictionary<string,Column> GetColumnsFromItem(IDictionary<string, JToken> item)
        {
            Contract.Requires(item != null, "item cannot be null");

            IDictionary<string, Column> cols = new Dictionary<string, Column>(defaultColumns);
            foreach (var kvp in item.Where(prop => !(prop.Key.Equals("status") || prop.Key.Equals("id") || prop.Key.Equals("guid") || prop.Key.Equals("timestamp"))))
            {
                cols.Add(kvp.Key, new Column(kvp.Key, ColumnTypeHelper.GetColumnTypeForClrType(((JValue)kvp.Value).Value.GetType()), true));
            }

            return cols;
        }

        private IDictionary<string, Column> DetermineNewColumns(IDictionary<string, Column> table, IDictionary<string, Column> item)
        {
            //all columns in item with name that do not exist in table 
            return item.Where(c => !table.ContainsKey(c.Key)).ToDictionary(x => x.Key, x => x.Value);
        }

        private async Task EnsureSchemaForTable(string tableName, Database db, IDictionary<string, Column> columns)
        {
            //check if database exists
            if (!await db.DoesTableExistAsync(tableName).ConfigureAwait(false))
            {
                Debug.WriteLine("Table doesn't exist: {0}", tableName);

                await db.CreateTableAsync(tableName, columns).ConfigureAwait(false);

                Debug.WriteLine("Table {0} created with columns {1}.", tableName, string.Join<Column>("\n", columns.Values));
                Debug.WriteLine("Creating index for table {0} on column 'id'", tableName);

                await db.CreateIndexAsync(tableName, "id").ConfigureAwait(false);
            }
            else
            {
                Debug.WriteLine("Table exists: {0}", tableName);
                Debug.WriteLine("Querying table structure for table: {0}", tableName);

                IDictionary<string, Column> tableColumns = await db.GetColumnsForTableAsync(tableName).ConfigureAwait(false);

                Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", tableColumns.Values));

                IDictionary<string, Column> newColumns = this.DetermineNewColumns(tableColumns, columns);
                foreach (var col in newColumns)
                {
                    await db.AddColumnForTableAsync(tableName, col.Value).ConfigureAwait(false);
                }
            }
        }
    }
}
