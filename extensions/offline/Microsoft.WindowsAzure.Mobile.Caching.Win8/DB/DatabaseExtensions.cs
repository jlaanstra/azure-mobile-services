using Newtonsoft.Json.Linq;
using SQLiteWinRT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public static class DatabaseExtensions
    {
        private static IDictionary<Type, Func<Statement, int, JValue>> typeToValue = new Dictionary<Type, Func<Statement, int, JValue>>()
        {
            { typeof(int), (stm, i) => new JValue(stm.GetIntAt(i)) },
            { typeof(uint), (stm, i) => new JValue(stm.GetIntAt(i)) },
            { typeof(long), (stm, i) => new JValue(stm.GetInt64At(i)) },
            { typeof(ulong), (stm, i) => new JValue(stm.GetInt64At(i)) },
            { typeof(short), (stm, i) => new JValue(stm.GetIntAt(i)) },
            { typeof(ushort), (stm, i) => new JValue(stm.GetIntAt(i)) },
            { typeof(sbyte), (stm, i) => new JValue(stm.GetIntAt(i)) },
            { typeof(byte), (stm, i) => new JValue(stm.GetIntAt(i)) },
            { typeof(char), (stm, i) => new JValue(stm.GetTextAt(i)[0]) },
            { typeof(string), (stm, i) => new JValue(stm.GetTextAt(i)) },
            { typeof(double), (stm, i) => new JValue(stm.GetDoubleAt(i)) },
            { typeof(decimal), (stm, i) => new JValue(stm.GetDoubleAt(i)) },
            { typeof(float), (stm, i) => new JValue(stm.GetDoubleAt(i)) },
            { typeof(bool), (stm, i) => new JValue(stm.GetIntAt(i) == 1) },
            { typeof(DateTime), (stm, i) => new JValue(DateTime.Parse(stm.GetTextAt(i))) },
            { typeof(DateTimeOffset), (stm, i) => new JValue(DateTimeOffset.Parse(stm.GetTextAt(i))) },
        };

        public static async Task<bool> DoesTableExistAsync(this Database This, string tableName)
        {
            Statement stm = await This.PrepareStatementAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = ? AND name = ?");
            stm.BindTextParameterAt(0, "table");
            stm.BindTextParameterAt(1, tableName);

            if (await stm.StepAsync())
            {
                return stm.GetIntAt(0) == 1;
            }
            return false;
        }

        public static Task CreateTableAsync(this Database This, string tableName, IList<Column> columns)
        {
            string sqlStatement = string.Format("CREATE TABLE IF NOT EXISTS {0} ({1})", tableName, string.Join(",", columns.Select(c => c.AsSQL())));
            return This.ExecuteStatementAsync(sqlStatement).AsTask();
        }

        public static Task CreateIndexAsync(this Database This, string tableName, string columnName)
        {
            string sqlStatement = string.Format("CREATE INDEX IF NOT EXISTS {0}_{1}_index ON {0} ({1})", tableName, columnName);
            return This.ExecuteStatementAsync(sqlStatement).AsTask();
        }

        public static async Task<IList<Column>> GetColumnsForTableAsync(this Database This, string tableName)
        {
            Statement stm = await This.PrepareStatementAsync(string.Format("pragma table_info({0})", tableName));

            List<Column> columns = new List<Column>();

            while(await stm.StepAsync())
            {
                columns.Add(new Column(stm.GetTextAt(0), stm.GetTextAt(1), stm.GetIntAt(2) != 0, stm.GetTextAt(3), stm.GetIntAt(4)));
            }

            return columns;            
        }

        public static Task AddColumnForTableAsync(this Database This, string tableName, Column column)
        {
            string sqlStatement = string.Format("ALTER TABLE {0} ADD COLUMN ({1})", tableName, column.AsSQL());
            return This.ExecuteStatementAsync(sqlStatement).AsTask();
        }

        public static async Task<IEnumerable<IDictionary<string, JToken>>> QueryAsync(this Database This, string sqlStatement, IList<Column> columns, int numberOfResult = 0)
        {
            Statement stm = await This.PrepareStatementAsync(sqlStatement);

            List<IDictionary<string, JToken>> results = new List<IDictionary<string, JToken>>(numberOfResult);

            while(await stm.StepAsync())
            {
                IDictionary<string, JToken> item = new Dictionary<string, JToken>();
                for(int i = 0; i < columns.Count; i++)
                {
                    Column col = columns[i];
                    Type type = col.GetClrDataType();
                    Func<Statement, int, JValue> valueEvaluator;
                    if(!typeToValue.TryGetValue(type, out valueEvaluator))
                    {
                        throw new InvalidOperationException("Type is unknown for a mapping to a sqlite type.");
                    }

                    item[col.Name] = valueEvaluator(stm, i);
                }

                results.Add(item);
            }

            return results;
        }

        public static async Task InsertIntoTableAsync(this Database This, string tableName, IList<Column> columns, IEnumerable<IDictionary<string, JToken>> data)
        {
            int count = columns.Count;

            StringBuilder builder = new StringBuilder(count * 2 + 1);
            builder.Append('(');
            for (int i = 0; i < count; i++)
            {
                builder.Append("? ");
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append(')');

            string insertStatement = builder.ToString();


            string sqlStatement = string.Format("INSERT OR REPLACE INTO {0} ('{1}') VALUES {1}", 
                tableName, 
                string.Join("','",columns.Select(c => c.Name)),
                string.Join(",", data.Select(_ => insertStatement))
                );

            Statement stm = await This.PrepareStatementAsync(sqlStatement);

            int itemIndex = 0;
            foreach(var item in data)
            {
                for(int i = 0; i < count; i++)
                {
                    int parameterIndex = itemIndex * count + i;

                    //TODO bindings
                }
            }
        }
    }

    public class Column
    {
        public Column(string name, string dataType, bool nullable, string defaultValue = null, int primaryKeyIndex = 0, bool isBuiltin = false)
        {
            this.Name = name;
            this.DataType = dataType;
            this.Nullable = nullable;
            this.DefaultValue = defaultValue;
            this.PrimaryKeyIndex = primaryKeyIndex;
            this.IsBuiltin = isBuiltin;
        }
        public string Name { get; private set; }

        public string DataType { get; private set; }

        public bool Nullable { get; private set; }

        public string DefaultValue { get; private set; }

        public int PrimaryKeyIndex { get; private set; }

        public bool IsBuiltin { get; private set; }

        public override string ToString()
        {
            return string.Format("Name: {0}, DataType: {1}, Nullable: {2}, DefaultValue: {3}, PrimaryKeyIndex: {4}");
        }
    }

    public static class ColumnExtensions
    {
        public static Type GetClrDataType(this Column This)
        {
            return ColumnTypeHelper.GetClrTypeForColumnType(This.DataType);
        }
        public static string AsSQL(this Column This)
        {
            return string.Format("{0} {1}{2}{3}{4}", 
                This.Name, 
                This.DataType, 
                This.PrimaryKeyIndex != 0 ? " PRIMARY KEY" : string.Empty,
                !This.Nullable ? " NOT NULL" : string.Empty,
                string.IsNullOrEmpty(This.DefaultValue) ? string.Format(" DEFAULT {0}",This.DefaultValue) : string.Empty
            );
        }
    }
}
