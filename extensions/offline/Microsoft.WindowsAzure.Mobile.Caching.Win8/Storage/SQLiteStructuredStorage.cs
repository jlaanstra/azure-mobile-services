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

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class SQLiteCacheStorage : IStructuredStorage
    {
        private string dbFile;
        private Database db;

        private List<Column> defaultColumns = new List<Column>()
        {
            new Column("id", ColumnTypeHelper.GetColumnTypeForClrType(typeof(long)), false, null, 0, true), // globally unique
            new Column("guid", ColumnTypeHelper.GetColumnTypeForClrType(typeof(Guid)), false, null, 1, true), // server id
            new Column("timestamp", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), false, null, 0, true), // timestamp of local item
            new Column("status", ColumnTypeHelper.GetColumnTypeForClrType(typeof(int)), false, null, 0, true), // status: unchanged:0 inserted:1 changed:2 deleted:3
        };

        public SQLiteCacheStorage(string name)
        {
            dbFile = string.Format("{0}.sqlite", name);
            db = new Database(ApplicationData.Current.LocalFolder, dbFile);
        }

        public async Task<IEnumerable<IDictionary<string, JToken>>> GetStoredData(string tableName, IQueryOptions query)
        {
            Debug.WriteLine("Retrieving data for table {0}, query: {1}", tableName, query.ToString());

            Debug.WriteLine("Opening database: {0}", this.dbFile);
            //let errors bubble up, nothing we can do here
            await db.OpenAsync(SqliteOpenMode.OpenOrCreateReadWrite);

            //check if database exists
            if(!await db.DoesTableExistAsync(tableName))
            {
                Debug.WriteLine("Table doesn't exist: {0}", tableName);
                return new Dictionary<string, JToken>[0];
            }

            Debug.WriteLine("Table exists: {0}", tableName);
            Debug.WriteLine("Querying table structure for table: {0}", tableName);

            IList<Column> columns = await db.GetColumnsForTableAsync(tableName);

            Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", columns));

            SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();
            visitor.VisitQueryOptions(query);

            var sqlStatement = string.Format(visitor.SqlStatement, tableName);

            Debug.WriteLine("Executing sql: {0}", sqlStatement);

            return await db.QueryAsync(sqlStatement, columns);
        }

        public async Task StoreData(string tableName, IEnumerable<IDictionary<string, JToken>> data)
        {
            Debug.WriteLine("Storing data for table {0}", tableName);

            if (!data.Any())
            {
                return;
            }

            Debug.WriteLine("Opening database: {0}", this.dbFile);
            //let errors bubble up, nothing we can do here
            await db.OpenAsync(SqliteOpenMode.OpenOrCreateReadWrite);
            
            IList<Column> columns = this.GetColumnsFromItem(data.FirstOrDefault());

            //check if database exists
            if (!await db.DoesTableExistAsync(tableName))
            {
                Debug.WriteLine("Table doesn't exist: {0}", tableName);

                await db.CreateTableAsync(tableName, columns);

                Debug.WriteLine("Table {0} created with columns {1}.", tableName, string.Join<Column>("\n", columns));
                Debug.WriteLine("Creating index for table {0} on column 'id'", tableName);

                await db.CreateIndexAsync(tableName, "id");
            }
            else
            {
                Debug.WriteLine("Table exists: {0}", tableName);
                Debug.WriteLine("Querying table structure for table: {0}", tableName);

                IList<Column> tableColumns = await db.GetColumnsForTableAsync(tableName);

                Debug.WriteLine("Table {0} has columns: {1}", tableName, string.Join<Column>("\n", tableColumns));

                IList<Column> newColumns = this.DetermineNewColumns(tableColumns, columns);
                foreach(var col in newColumns)
                {
                    await db.AddColumnForTableAsync(tableName, col);
                }
            }

            StringBuilder sql = new StringBuilder();
            sql.Append("INSERT OR REPLACE INTO ");
            sql.Append(tableName);
            sql.Append(string.Format(" ('{0}')", string.Join("','", mapping.Columns.Select(c => c.Name))));
            sql.Append(" VALUES ");
            foreach (var item in data)
            {
                sql.Append("('");
                foreach (var col in mapping.Columns)
                {
                    JValue val = item[col.Name] as JValue;
                    if (val == null)
                    {
                        sql.Append("NULL");
                    }
                    else
                    {
                        sql.Append(col.IsPK ? val.Value.ToString().ToLowerInvariant() : val.Value);
                    }
                    sql.Append("','");
                }
                sql.Remove(sql.Length - 3, 3);
                sql.Append("'),");
            }
            sql.Remove(sql.Length - 1, 1);

            Debug.WriteLine("Executing sql: {0}", sql.ToString());

            SQLiteCommand command = db.CreateCommand(sql.ToString(), tableName);
            await Task.Run(() => command.ExecuteNonQuery());
        }

        public async Task UpdateData(string tableName, string guid, IDictionary<string, JToken> data)
        {
            Debug.WriteLine("Update data {0} for table {1}", guid, tableName);

            if (data == null)
            {
                return;
            }

            // there will always be one item, since otherwise we would already have returned
            TableMapping mapping = this.DeriveSchema(tableName, data);

            await Task.Run(() => db.CreateTable(mapping));

            Debug.WriteLine("Table {0}, using mapping: {1}", tableName, string.Join<TableMapping.Column>(",", mapping.Columns));

            StringBuilder sql = new StringBuilder();
            sql.Append("UPDATE ");
            sql.Append(tableName);
            sql.Append(" SET ");
            foreach (var col in mapping.Columns)
            {
                sql.Append(col.Name);
                sql.Append(" = '");
                JValue val = data[col.Name] as JValue;
                if (val == null)
                {
                    sql.Append("NULL");
                }
                else
                {
                    sql.Append(col.IsPK ? val.Value.ToString().ToLowerInvariant() : val.Value);
                }
                sql.Append("',");
            }
            sql.Remove(sql.Length - 1, 1);
            sql.Append(" WHERE guid = '");
            sql.Append(guid);
            sql.Append("'");

            Debug.WriteLine("Executing sql: {0}", sql.ToString());

            SQLiteCommand command = db.CreateCommand(sql.ToString(), tableName);
            await Task.Run(() => command.ExecuteNonQuery());
        }

        public async Task RemoveStoredData(string tableName, IEnumerable<string> guids)
        {
            Debug.WriteLine(string.Format("Removing data for table {0}, guids: {1}", tableName, string.Join(",", guids)));

            if (!guids.Any())
            {
                return;
            }

            StringBuilder sql = new StringBuilder();
            sql.Append("DELETE FROM ");
            sql.Append(tableName);
            sql.Append(" WHERE guid IN ('");
            foreach (string guid in guids)
            {
                sql.Append(guid.ToLowerInvariant());
                sql.Append("','");
            }
            sql.Remove(sql.Length - 3, 3);
            sql.Append("')");

            Debug.WriteLine("Executing sql: {0}", sql.ToString());

            SQLiteCommand command = db.CreateCommand(sql.ToString());
            await Task.Run(() => command.ExecuteNonQuery());
        }

        private IList<Column> GetColumnsFromItem(IDictionary<string, JToken> item)
        {
            Contract.Requires(item != null, "item cannot be null");

            List<Column> cols = new List<Column>();
            cols.AddRange(this.defaultColumns);
            foreach (var kvp in item.Where(prop => !(prop.Key.Equals("status") || prop.Key.Equals("id") || prop.Key.Equals("guid") || prop.Key.Equals("timestamp"))))
            {
                cols.Add(new Column(kvp.Key, ColumnTypeHelper.GetColumnTypeForClrType(((JValue)kvp.Value).Value.GetType()), true));
            }

            return cols;
        }

        private IList<Column> DetermineNewColumns(IList<Column> table, IList<Column> item)
        {
            //all columns in item with name that do not exist in table 
            return item.Where(c => !table.Any(t => t.Name.Equals(c.Name))).ToList();
        }

        private Type JTokenToClrType(JToken token)
        {
            JValue val = token as JValue;
            switch (val.Type)
            {
                case JTokenType.String:
                    return typeof(string);
                case JTokenType.Date:
                    return typeof(DateTime);
                case JTokenType.Float:
                case JTokenType.Integer:
                    return typeof(double);
                case JTokenType.Boolean:
                    return typeof(string);
                default:
                    Debug.WriteLine("Encountered token type {0}.", val.Type);
                    return typeof(string);
            }
        }

        private Type StringToClrType(string typeString)
        {
            switch (typeString)
            {
                case "datetime":
                    return typeof(DateTime);
                case "float":
                    return typeof(double);
                case "integer":
                    return typeof(int);
                case "bigint":
                    return typeof(long);
                case "blob":
                    return typeof(byte[]);
                default:
                    Debug.WriteLine("Encountered type string {0}.", typeString);
                    return typeof(string);
            }
        }
    }
}
