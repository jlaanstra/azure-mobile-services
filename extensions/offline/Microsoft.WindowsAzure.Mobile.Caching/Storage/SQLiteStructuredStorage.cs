using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Diagnostics.Contracts;

using System.Runtime.InteropServices;
using SQLitePCL;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class SQLiteStructuredStorage : IStructuredStorage
    {
        private string dbFile;
        private SQLiteConnection db;
        private int databaseThreadId;
        private static Task completed = Task.FromResult(0);

        internal IDictionary<string, Column> defaultColumns = new Dictionary<string, Column>()
        {
            { "id", new Column("id", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), false, null, 1, true) }, // globally unique
            { "__version", new Column("__version", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), true, null, 0, true) }, // version of local item
            { "status", new Column("status", ColumnTypeHelper.GetColumnTypeForClrType(typeof(int)), false, null, 0, true) }, // status: unchanged:0 inserted:1 changed:2 deleted:3
        };

        public SQLiteStructuredStorage(string name)
        {
            dbFile = string.Format("{0}.sqlite", name);
        }

        public Task<IDisposable> Open()
        {
            Debug.WriteLine("Opening database: {0}", this.dbFile);
            db = new SQLiteConnection(dbFile);

#if DEBUG
            this.databaseThreadId = Environment.CurrentManagedThreadId;
#endif
            return Task.FromResult<IDisposable>(new Disposable(() =>
            {
                if (db != null)
                {
                    db.Dispose();
                    db = null;
                }
            }));
        }

        /// <summary>
        /// Gets the stored data.
        /// This method should not alter the database in any way.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="query">The query.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public Task<JArray> GetStoredData(string tableName, IQueryOptions query)
        {
            Debug.WriteLine("Retrieving data for table {0}, query: {1}", tableName, query.ToString());

            EnsureDatabaseThread();

            try
            {
                //check if database exists
                if (!db.DoesTableExist(tableName))
                {
                    Debug.WriteLine("Table doesn't exist: {0}", tableName);
                    return Task.FromResult(new JArray());
                }

                Debug.WriteLine("Table exists: {0}", tableName);
                Debug.WriteLine("Querying table structure for table: {0}", tableName);

                IDictionary<string, Column> columns = db.GetColumnsForTable(tableName);

                Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", columns.Values));

                SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();
                visitor.VisitQueryOptions(query);

                var sqlStatement = string.Format(visitor.SqlStatement, tableName);

                Debug.WriteLine("Executing sql: {0}", sqlStatement);

                return Task.FromResult(db.Query(sqlStatement, columns));
            }
            catch (SQLiteException ex)
            {
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", ex.Message));
            }
        }

        public Task StoreData(string tableName, JArray data, bool overwrite = true)
        {
            Debug.WriteLine("Storing data for table {0}", tableName);

            if (!data.Any())
            {
                return completed;
            }

            EnsureDatabaseThread();

            // the actual columns for the item
            IDictionary<string, Column> columns = this.GetColumnsFromItems(data);
            this.EnsureSchemaForTable(tableName, db, columns);

            try
            {
                Debug.WriteLine("Inserting...");

                db.InsertIntoTable(tableName, columns, data.OfType<JObject>(), overwrite);
                return completed;
            }
            catch (SQLiteException ex)
            {
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", ex.Message));
            }
        }

        public Task UpdateData(string tableName, JArray data)
        {
            Debug.WriteLine("Update table {0}", tableName);

            if (!data.Any())
            {
                return completed;
            }

            EnsureDatabaseThread();

            // the actual columns for the item
            IDictionary<string, Column> columns = this.GetColumnsFromItems(data);
            this.EnsureSchemaForTable(tableName, db, columns);

            try
            {
                Debug.WriteLine("Updating...");

                foreach (JObject item in data)
                {
                    db.UpdateInTable(tableName, columns, item);
                }
                return completed;
            }
            catch (SQLiteException ex)
            {
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", ex.Message));
            }
        }

        public Task RemoveStoredData(string tableName, JArray ids)
        {
            Debug.WriteLine(string.Format("Removing data for table {0}, ids: {1}", tableName, string.Join(",", ids)));

            if (!ids.Any())
            {
                return completed;
            }

            EnsureDatabaseThread();

            try
            {
                Debug.WriteLine("Deleting...");

                db.DeleteFromTable(tableName, ids.Select(t => t.ToString()));
                return completed;
            }
            catch (SQLiteException ex)
            {
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", ex.Message));
            }
        }

        public async Task Purge()
        {
            JArray arr = this.db.Query("SELECT name FROM sqlite_master WHERE type = 'table'", new Dictionary<string, Column>() { { "name", new Column("name", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), false) } });
            foreach(JObject obj in arr.OfType<JObject>())
            {
                this.db.DropTable(obj.Value<string>("name") ?? string.Empty);
            }
        }

        internal IDictionary<string, Column> GetColumnsFromItems(JArray items)
        {
            Contract.Requires(items != null, "item cannot be null");

            IDictionary<string, Column> cols = new Dictionary<string, Column>();

            IEnumerator<IDictionary<string, JToken>> objects = items.OfType<IDictionary<string, JToken>>().GetEnumerator();
            objects.MoveNext();
            ICollection<string> columnsToDetermine = objects.Current.Keys;

            do
            {
                IList<string> nullColumns = new List<string>();
                foreach (var key in columnsToDetermine)
                {
                    JToken token;
                    if(defaultColumns.ContainsKey(key))
                    {
                        cols.Add(key, defaultColumns[key]);
                    }
                    else if (objects.Current.TryGetValue(key, out token))
                    {
                        JValue val = token as JValue;
                        if (val != null)
                        {
                            if (val.Value == null)
                            {
                                nullColumns.Add(key);
                            }
                            else
                            {
                                cols.Add(key, new Column(key, ColumnTypeHelper.GetColumnTypeForClrType(val.Value.GetType()), true));
                            }
                        }
                    }
                }
                columnsToDetermine = nullColumns;
            }
            while (columnsToDetermine.Count > 0 && objects.MoveNext());

            return cols;
        }


        /// <summary>
        /// Determines the new columns on the item. Missing columns should be ignored.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        internal IDictionary<string, Column> DetermineNewColumns(IDictionary<string, Column> table, IDictionary<string, Column> item)
        {
            //all columns in item with name that do not exist in table 
            return item.Where(c => !table.ContainsKey(c.Key)).ToDictionary(x => x.Key, x => x.Value);
        }

        private void EnsureSchemaForTable(string tableName, SQLiteConnection db, IDictionary<string, Column> columns)
        {
            EnsureDatabaseThread();

            try
            {
                //check if database exists
                if (!db.DoesTableExist(tableName))
                {
                    Debug.WriteLine("Table doesn't exist: {0}", tableName);

                    db.CreateTable(tableName, columns);

                    Debug.WriteLine("Table {0} created with columns {1}.", tableName, string.Join<Column>("\n", columns.Values));
                    Debug.WriteLine("Creating index for table {0} on column 'id'", tableName);

                    db.CreateIndex(tableName, "id");
                }
                else
                {
                    Debug.WriteLine("Table exists: {0}", tableName);
                    Debug.WriteLine("Querying table structure for table: {0}", tableName);

                    IDictionary<string, Column> tableColumns = db.GetColumnsForTable(tableName);

                    Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", tableColumns.Values));

                    IDictionary<string, Column> newColumns = this.DetermineNewColumns(tableColumns, columns);
                    foreach (var col in newColumns)
                    {
                        db.AddColumnForTable(tableName, col.Value);
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw new Exception(string.Format("Error occurred during interaction with the local database: {0}", ex.Message));
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
