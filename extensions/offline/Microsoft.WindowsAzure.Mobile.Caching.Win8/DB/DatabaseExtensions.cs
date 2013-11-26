using Newtonsoft.Json.Linq;
using SQLiteWinRT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            { typeof(DateTime), (stm, i) => 
            {
                DateTime datetime;
                if(DateTime.TryParse(stm.GetTextAt(i), out datetime))
                {
                    return new JValue(datetime);
                }
                else
                {
                    return null;
                }
            } },
            { typeof(DateTimeOffset), (stm, i) => 
            {
                DateTimeOffset datetime;
                if (DateTimeOffset.TryParse(stm.GetTextAt(i), out datetime))
                {
                    return new JValue(datetime);
                }
                else
                {
                    return null;
                }
            } },
            { typeof(Guid), (stm, i) => new JValue(stm.GetTextAt(i)) },
        };

        private static IDictionary<Type, Action<Statement, int, JValue>> bindToValue = new Dictionary<Type, Action<Statement, int, JValue>>()
        {
            { typeof(int), (stm, i, token) =>
            {
                stm.BindIntParameterAt(i, Convert.ToInt32(token.Value));
            } },
            //uint can overflow int
            { typeof(uint), (stm, i, token) => stm.BindInt64ParameterAt(i, Convert.ToInt64(token.Value)) },
            { typeof(long), (stm, i, token) => stm.BindInt64ParameterAt(i, Convert.ToInt64(token.Value)) },
            { typeof(ulong), (stm, i, token) => stm.BindInt64ParameterAt(i, Convert.ToInt64(token.Value)) },
            { typeof(short), (stm, i, token) => stm.BindIntParameterAt(i, Convert.ToInt32(token.Value)) },
            { typeof(ushort), (stm, i, token) => stm.BindIntParameterAt(i, Convert.ToInt32(token.Value)) },
            { typeof(sbyte), (stm, i, token) => stm.BindIntParameterAt(i, Convert.ToInt32(token.Value)) },
            { typeof(byte), (stm, i, token) => stm.BindIntParameterAt(i, Convert.ToInt32(token.Value)) },
            { typeof(char), (stm, i, token) => stm.BindTextParameterAt(i, token.Value.ToString()) },
            { typeof(string), (stm, i, token) => stm.BindTextParameterAt(i, token.Value.ToString()) },
            { typeof(double), (stm, i, token) => stm.BindDoubleParameterAt(i, Convert.ToDouble(token.Value)) },
            { typeof(decimal), (stm, i, token) => stm.BindIntParameterAt(i, Convert.ToInt32(token.Value)) },
            { typeof(float), (stm, i, token) => stm.BindDoubleParameterAt(i, Convert.ToSingle(token.Value)) },
            { typeof(bool), (stm, i, token) => stm.BindIntParameterAt(i, Convert.ToBoolean(token.Value) ? 1 : 0) },
            { typeof(DateTime), (stm, i, token) => stm.BindTextParameterAt(i, token.Value.ToString()) },
            { typeof(DateTimeOffset), (stm, i, token) => stm.BindTextParameterAt(i, token.Value.ToString()) },
            { typeof(Guid), (stm, i, token) => stm.BindTextParameterAt(i, token.Value.ToString()) },
        };

        public static async Task<bool> DoesTableExistAsync(this Database This, string tableName)
        {
            using (Statement stm = await This.PrepareStatementAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = ? AND name = ?"))
            {
                stm.BindTextParameterAt(1, "table");
                stm.BindTextParameterAt(2, tableName);

                if (await stm.StepAsync())
                {
                    return stm.GetIntAt(0) == 1;
                }
                return false;
            }
        }

        public static Task CreateTableAsync(this Database This, string tableName, IDictionary<string, Column> columns)
        {
            string sqlStatement = string.Format("CREATE TABLE IF NOT EXISTS {0} ({1})", tableName, string.Join(",", columns.Values.Select(c => c.AsSQL())));

            Debug.WriteLine(sqlStatement);

            return This.ExecuteStatementAsync(sqlStatement).AsTask();
        }

        public static Task CreateIndexAsync(this Database This, string tableName, string columnName)
        {
            string sqlStatement = string.Format("CREATE INDEX IF NOT EXISTS {0}_{1}_index ON {0} (\"{1}\")", tableName, columnName);

            Debug.WriteLine(sqlStatement);

            return This.ExecuteStatementAsync(sqlStatement).AsTask();
        }

        public static async Task<IDictionary<string, Column>> GetColumnsForTableAsync(this Database This, string tableName)
        {
            using (Statement stm = await This.PrepareStatementAsync(string.Format("pragma table_info({0})", tableName)))
            {
                IDictionary<string, Column> columns = new Dictionary<string, Column>();

                while (await stm.StepAsync())
                {
                    columns.Add(stm.GetTextAt(1), new Column(stm.GetTextAt(1), stm.GetTextAt(2), stm.GetIntAt(3) != 0, stm.GetTextAt(4), stm.GetIntAt(5)));
                }

                return columns;
            }
        }

        public static Task AddColumnForTableAsync(this Database This, string tableName, Column column)
        {
            string sqlStatement = string.Format("ALTER TABLE {0} ADD COLUMN {1}", tableName, column.AsSQL());
            return This.ExecuteStatementAsync(sqlStatement).AsTask();
        }

        public static async Task<JArray> QueryAsync(this Database This, string sqlStatement, IDictionary<string, Column> columns)
        {
            using (Statement stm = await This.PrepareStatementAsync(sqlStatement))
            {
                JArray results = new JArray();

                while (await stm.StepAsync())
                {
                    JObject item = new JObject();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        Column col = columns[stm.GetColumnName(i)];
                        Type type = col.GetClrDataType();
                        Func<Statement, int, JValue> valueEvaluator;
                        if (!typeToValue.TryGetValue(type, out valueEvaluator))
                        {
                            throw new InvalidOperationException("Type is unknown for a mapping to a sqlite type.");
                        }

                        if (stm.GetColumnType(i) != ColumnType.Null)
                        {
                            item[col.Name] = valueEvaluator(stm, i);
                        }
                        else
                        {
                            item[col.Name] = null;
                        }
                    }

                    results.Add(item);
                }

                return results;
            }
        }

        public static async Task InsertIntoTableAsync(this Database This, string tableName, IDictionary<string, Column> columns, IDictionary<string, JToken> data)
        {
            int count = data.Count;

            StringBuilder builder = new StringBuilder(count * 2 - 1);
            for (int i = 0; i < count; i++)
            {
                builder.Append("?,");
            }
            builder.Remove(builder.Length - 1, 1);

            string insertStatement = builder.ToString();

            string sqlStatement = string.Format("INSERT OR REPLACE INTO {0} ('{1}') VALUES ({2})",
                tableName,
                string.Join("','", data.Keys),
                insertStatement
                );

            Debug.WriteLine(sqlStatement);

            using (Statement stm = await This.PrepareStatementAsync(sqlStatement))
            {
                stm.BindDataToStatement(columns, data);

                await stm.StepAsync();
            }
        }

        public static async Task UpdateInTableAsync(this Database This, string tableName, IDictionary<string, Column> columns, IDictionary<string, JToken> data)
        {
            int count = data.Count;

            StringBuilder builder = new StringBuilder();
            foreach (var prop in data)
            {
                builder.Append(prop.Key);
                builder.Append(" = ");
                builder.Append("?,");
            }
            builder.Remove(builder.Length - 1, 1);

            string updateStatement = builder.ToString();

            string sqlStatement = string.Format("UPDATE {0} SET {1} WHERE guid = '{2}'",
                tableName,
                updateStatement,
                data["guid"]
                );

            Debug.WriteLine(sqlStatement);

            using (Statement stm = await This.PrepareStatementAsync(sqlStatement))
            {
                stm.BindDataToStatement(columns, data);

                await stm.StepAsync();
            }
        }

        private static void BindDataToStatement(this Statement This, IDictionary<string, Column> columns, IDictionary<string, JToken> data)
        {
            int j = 1;
            foreach (var prop in data)
            {
                Column c = columns[prop.Key];
                JToken value = prop.Value;

                if (value != null)
                {
                    //make sure string are inserted lowercase
                    if (c.IsBuiltin && c.GetClrDataType() == typeof(Guid))
                    {
                        value = new JValue(value.ToString().ToLowerInvariant());
                    }

                    Action<Statement, int, JValue> bind;
                    if (!bindToValue.TryGetValue(c.GetClrDataType(), out bind))
                    {
                        throw new InvalidOperationException("Type is unknown for a mapping to a sqlite type.");
                    }
                    bind(This, j, (JValue)value);
                }
                else
                {
                    This.BindNullParameterAt(j);
                }
                j++;
            }
        }

        public static async Task DeleteFromTableAsync(this Database This, string tableName, IEnumerable<string> guids)
        {
            int count = guids.Count();

            StringBuilder builder = new StringBuilder(count * 2 + 1);
            builder.Append('(');
            for (int i = 0; i < count; i++)
            {
                builder.Append("?,");
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append(')');

            string updateStatement = builder.ToString();

            string sqlStatement = string.Format("DELETE FROM {0} WHERE guid IN {1}",
                tableName,
                updateStatement
                );

            Debug.WriteLine(sqlStatement);

            using (Statement stm = await This.PrepareStatementAsync(sqlStatement))
            {
                IEnumerator<string> e = guids.GetEnumerator();
                for (int i = 0; i < count; i++)
                {
                    e.MoveNext();
                    stm.BindTextParameterAt(i + 1, e.Current.ToLowerInvariant());
                }

                await stm.StepAsync();
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
            return string.Format("Name: {0}, DataType: {1}, Nullable: {2}, DefaultValue: {3}, PrimaryKeyIndex: {4}", Name, DataType, Nullable, DefaultValue, PrimaryKeyIndex);
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
            return string.Format("\"{0}\" \"{1}\"{2}{3}{4}",
                This.Name,
                This.DataType,
                This.PrimaryKeyIndex != 0 ? " PRIMARY KEY" : string.Empty,
                !This.Nullable ? " NOT NULL" : string.Empty,
                !string.IsNullOrEmpty(This.DefaultValue) ? string.Format(" DEFAULT {0}", This.DefaultValue) : string.Empty
            );
        }
    }
}
