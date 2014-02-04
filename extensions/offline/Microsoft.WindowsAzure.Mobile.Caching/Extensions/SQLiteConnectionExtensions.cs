using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLitePCL;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public static class SQLiteConnectionExtensions
    {
        private static IDictionary<Type, Func<ISQLiteStatement, int, JValue>> typeToValue = new Dictionary<Type, Func<ISQLiteStatement, int, JValue>>()
        {
            { typeof(int), (stm, i) => new JValue((int)stm[i]) },
            { typeof(uint), (stm, i) => new JValue((int)stm[i]) },
            { typeof(long), (stm, i) => new JValue((int)stm[i]) },
            { typeof(ulong), (stm, i) => new JValue((int)stm[i]) },
            { typeof(short), (stm, i) => new JValue((int)stm[i]) },
            { typeof(ushort), (stm, i) => new JValue((int)stm[i]) },
            { typeof(sbyte), (stm, i) => new JValue((int)stm[i]) },
            { typeof(byte), (stm, i) => new JValue((int)stm[i]) },
            { typeof(char), (stm, i) => new JValue(stm[i].ToString()[0]) },
            { typeof(string), (stm, i) => new JValue(stm[i].ToString()) },
            { typeof(double), (stm, i) => new JValue((double)stm[i]) },
            { typeof(decimal), (stm, i) => new JValue((double)stm[i]) },
            { typeof(float), (stm, i) => new JValue((double)stm[i]) },
            { typeof(bool), (stm, i) => new JValue((int)stm[i] == 1) },
            { typeof(DateTime), (stm, i) => 
            {
                DateTime datetime;
                if(DateTime.TryParse(stm[i].ToString(), out datetime))
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
                if (DateTimeOffset.TryParse(stm[i].ToString(), out datetime))
                {
                    return new JValue(datetime);
                }
                else
                {
                    return null;
                }
            } },
            { typeof(Guid), (stm, i) => new JValue(stm[i].ToString()) },
        };

        private static IDictionary<Type, Action<ISQLiteStatement, int, JValue>> bindToValue = new Dictionary<Type, Action<ISQLiteStatement, int, JValue>>()
        {
            { typeof(int), (stm, i, token) =>
            {
                stm.Bind(i, Convert.ToInt32(token.Value));
            } },
            //uint can overflow int
            { typeof(uint), (stm, i, token) => stm.Bind(i, Convert.ToInt64(token.Value)) },
            { typeof(long), (stm, i, token) => stm.Bind(i, Convert.ToInt64(token.Value)) },
            { typeof(ulong), (stm, i, token) => stm.Bind(i, Convert.ToInt64(token.Value)) },
            { typeof(short), (stm, i, token) => stm.Bind(i, Convert.ToInt32(token.Value)) },
            { typeof(ushort), (stm, i, token) => stm.Bind(i, Convert.ToInt32(token.Value)) },
            { typeof(sbyte), (stm, i, token) => stm.Bind(i, Convert.ToInt32(token.Value)) },
            { typeof(byte), (stm, i, token) => stm.Bind(i, Convert.ToInt32(token.Value)) },
            { typeof(char), (stm, i, token) => stm.Bind(i, token.Value.ToString()) },
            { typeof(string), (stm, i, token) => stm.Bind(i, token.Value.ToString()) },
            { typeof(double), (stm, i, token) => stm.Bind(i, Convert.ToDouble(token.Value)) },
            { typeof(decimal), (stm, i, token) => stm.Bind(i, Convert.ToInt32(token.Value)) },
            { typeof(float), (stm, i, token) => stm.Bind(i, Convert.ToSingle(token.Value)) },
            { typeof(bool), (stm, i, token) => stm.Bind(i, Convert.ToBoolean(token.Value) ? 1 : 0) },
            { typeof(DateTime), (stm, i, token) => stm.Bind(i, token.Value.ToString()) },
            { typeof(DateTimeOffset), (stm, i, token) => stm.Bind(i, token.Value.ToString()) },
            { typeof(Guid), (stm, i, token) => stm.Bind(i, token.Value.ToString()) },
        };

        public static bool DoesTableExist(this SQLiteConnection This, string tableName)
        {
            string sqlStatement = "SELECT COUNT(*) FROM sqlite_master WHERE type = ? AND name = ?";

            Debug.WriteLine(sqlStatement);

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                stm.Bind(1, "table");
                stm.Bind(2, tableName);

                if (stm.Step() == SQLiteResult.OK)
                {
                    return (int)stm[0] == 1;
                }
                return false;
            }
        }

        public static void CreateTable(this SQLiteConnection This, string tableName, IDictionary<string, Column> columns)
        {
            string sqlStatement = string.Format("CREATE TABLE IF NOT EXISTS {0} ({1})", tableName, string.Join(",", columns.Values.Select(c => c.AsSQL())));

            Debug.WriteLine(sqlStatement);

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                stm.Step();
            }
        }

        public static void CreateIndex(this SQLiteConnection This, string tableName, string columnName)
        {
            string sqlStatement = string.Format("CREATE INDEX IF NOT EXISTS {0}_{1}_index ON {0} (\"{1}\")", tableName, columnName);

            Debug.WriteLine(sqlStatement);

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                stm.Step();
            }
        }

        public static IDictionary<string, Column> GetColumnsForTable(this SQLiteConnection This, string tableName)
        {
            using (ISQLiteStatement stm = This.Prepare(string.Format("pragma table_info({0})", tableName)))
            {
                IDictionary<string, Column> columns = new Dictionary<string, Column>();

                while (stm.Step() == SQLiteResult.OK)
                {
                    columns.Add(stm[1].ToString(), new Column(stm[1].ToString(), stm[2].ToString(), (int)stm[3] != 0, stm[4].ToString(), (int)stm[5]));
                }

                return columns;
            }
        }

        public static void AddColumnForTable(this SQLiteConnection This, string tableName, Column column)
        {
            string sqlStatement = string.Format("ALTER TABLE {0} ADD COLUMN {1}", tableName, column.AsSQL());
            
            Debug.WriteLine(sqlStatement);

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                stm.Step();
            }
        }

        public static JArray Query(this SQLiteConnection This, string sqlStatement, IDictionary<string, Column> columns)
        {
            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                JArray results = new JArray();

                while (stm.Step() == SQLiteResult.OK)
                {
                    JObject item = new JObject();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        Column col = columns[stm.ColumnName(i)];
                        Type type = col.GetClrDataType();
                        Func<ISQLiteStatement, int, JValue> valueEvaluator;
                        if (!typeToValue.TryGetValue(type, out valueEvaluator))
                        {
                            throw new InvalidOperationException("Type is unknown for a mapping to a sqlite type.");
                        }
                        item[col.Name] = valueEvaluator(stm, i);
                    }

                    results.Add(item);
                }

                return results;
            }
        }

        public static void InsertIntoTable(this SQLiteConnection This, string tableName, IDictionary<string, Column> columns, IDictionary<string, JToken> data)
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

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                stm.BindDataToStatement(columns, data);

                stm.Step();
            }
        }

        public static void UpdateInTable(this SQLiteConnection This, string tableName, IDictionary<string, Column> columns, IDictionary<string, JToken> data)
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

            string sqlStatement = string.Format("UPDATE {0} SET {1} WHERE id = '{2}'",
                tableName,
                updateStatement,
                data["id"]
                );

            Debug.WriteLine(sqlStatement);

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                stm.BindDataToStatement(columns, data);

                stm.Step();
            }
        }

        private static void BindDataToStatement(this ISQLiteStatement This, IDictionary<string, Column> columns, IDictionary<string, JToken> data)
        {
            int j = 1;
            foreach (var prop in data)
            {
                Column c = columns[prop.Key];
                JValue value = prop.Value as JValue;

                if (value != null && value.Value != null)
                {
                    //make sure string ids are inserted lowercase
                    if (c.IsBuiltin && c.GetClrDataType() == typeof(Guid))
                    {
                        value = new JValue(value.Value.ToString().ToLowerInvariant());
                    }

                    Action<ISQLiteStatement, int, JValue> bind;
                    if (!bindToValue.TryGetValue(c.GetClrDataType(), out bind))
                    {
                        throw new InvalidOperationException("Type is unknown for a mapping to a sqlite type.");
                    }
                    bind(This, j, value);
                }
                else
                {
                    This.Bind(j, null);
                }
                j++;
            }
        }

        public static void DeleteFromTable(this SQLiteConnection This, string tableName, IEnumerable<string> ids)
        {
            int count = ids.Count();

            StringBuilder builder = new StringBuilder(count * 2 + 1);
            builder.Append('(');
            for (int i = 0; i < count; i++)
            {
                builder.Append("?,");
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append(')');

            string updateStatement = builder.ToString();

            string sqlStatement = string.Format("DELETE FROM {0} WHERE id IN {1}",
                tableName,
                updateStatement
                );

            Debug.WriteLine(sqlStatement);

            using (ISQLiteStatement stm = This.Prepare(sqlStatement))
            {
                IEnumerator<string> e = ids.GetEnumerator();
                for (int i = 0; i < count; i++)
                {
                    e.MoveNext();
                    stm.Bind(i + 1, e.Current.ToLowerInvariant());
                }

                stm.Step();
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
