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
#if DEBUG
        private int databaseThreadId;
#endif

        private IDictionary<string, Column> defaultColumns = new Dictionary<string, Column>()
        {
            { "id", new Column("id", ColumnTypeHelper.GetColumnTypeForClrType(typeof(Guid)), false, null, 1, true) }, // globally unique
            { "__version", new Column("__version", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), true, null, 0, true) }, // version of local item
            { "status", new Column("status", ColumnTypeHelper.GetColumnTypeForClrType(typeof(int)), false, null, 0, true) }, // status: unchanged:0 inserted:1 changed:2 deleted:3
        };

        public SQLiteCacheStorage(string name)
        {
            dbFile = string.Format("{0}.sqlite", name);
        }

        public async Task<IDisposable> OpenAsync()
        {
            db = new Database(ApplicationData.Current.LocalFolder, dbFile);

#if DEBUG
            this.databaseThreadId = Environment.CurrentManagedThreadId;
#endif

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

            EnsureDatabaseThread();

            try
            {
                //check if database exists
                if (!await db.DoesTableExistAsync(tableName))
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

                return await db.QueryAsync(sqlStatement, columns);
            }
            catch (Exception ex)
            {
                var result = Database.GetSqliteErrorCode(ex.HResult);
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", result));
            }
        }

        public async Task StoreData(string tableName, JArray data)
        {
            Debug.WriteLine("Storing data for table {0}", tableName);

            if (!data.Any())
            {
                return;
            }

            EnsureDatabaseThread();

            // the actual columns for the item
            IDictionary<string, Column> columns = this.GetColumnsFromItem(data.First as JObject);
            await this.EnsureSchemaForTable(tableName, db, columns);

            try
            {
                Debug.WriteLine("Inserting...");

                foreach (JObject item in data)
                {
                    await db.InsertIntoTableAsync(tableName, columns, item);
                }
            }
            catch (Exception ex)
            {
                var result = Database.GetSqliteErrorCode(ex.HResult);
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", result));
            }
        }

        public async Task UpdateData(string tableName, JArray data)
        {
            Debug.WriteLine("Update table {0}", tableName);

            if (!data.Any())
            {
                return;
            }

            EnsureDatabaseThread();

            // the actual columns for the item
            IDictionary<string, Column> columns = this.GetColumnsFromItem(data.First as JObject);
            await this.EnsureSchemaForTable(tableName, db, columns);

            try
            {
                Debug.WriteLine("Updating...");

                foreach (JObject item in data)
                {
                    await db.UpdateInTableAsync(tableName, columns, item);
                }
            }
            catch (Exception ex)
            {
                var result = Database.GetSqliteErrorCode(ex.HResult);
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", result));
            }
        }

        public async Task RemoveStoredData(string tableName, IEnumerable<string> guids)
        {
            Debug.WriteLine(string.Format("Removing data for table {0}, guids: {1}", tableName, string.Join(",", guids)));

            if (!guids.Any())
            {
                return;
            }

            EnsureDatabaseThread();

            try
            {
                Debug.WriteLine("Deleting...");

                await db.DeleteFromTableAsync(tableName, guids);
            }
            catch (Exception ex)
            {
                var result = Database.GetSqliteErrorCode(ex.HResult);
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", result));
            }
        }

        private IDictionary<string, Column> GetColumnsFromItem(IDictionary<string, JToken> item)
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
            EnsureDatabaseThread();

            Exception exception = null;

            try
            {
                //check if database exists
                if (!await db.DoesTableExistAsync(tableName))
                {
                    Debug.WriteLine("Table doesn't exist: {0}", tableName);

                    await db.CreateTableAsync(tableName, columns);

                    Debug.WriteLine("Table {0} created with columns {1}.", tableName, string.Join<Column>("\n", columns.Values));
                    Debug.WriteLine("Creating index for table {0} on column 'id'", tableName);

                    await db.CreateIndexAsync(tableName, "id");
                }
                else
                {
                    Debug.WriteLine("Table exists: {0}", tableName);
                    Debug.WriteLine("Querying table structure for table: {0}", tableName);

                    IDictionary<string, Column> tableColumns = await db.GetColumnsForTableAsync(tableName);

                    Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", tableColumns.Values));

                    IDictionary<string, Column> newColumns = this.DetermineNewColumns(tableColumns, columns);
                    foreach (var col in newColumns)
                    {
                        await db.AddColumnForTableAsync(tableName, col.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                var result = Database.GetSqliteErrorCode(ex.HResult);
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", result));
            }
        }

        [Conditional("Debug")]
        private void EnsureDatabaseThread()
        {
            if (this.databaseThreadId != Environment.CurrentManagedThreadId)
            {
                throw new InvalidOperationException(string.Format("Database accessed from thread with id {0} while id {1} was expected.", Environment.CurrentManagedThreadId, this.databaseThreadId));
            }
        }
    }
}
