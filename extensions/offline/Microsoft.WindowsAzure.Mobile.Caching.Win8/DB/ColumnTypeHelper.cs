using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class ColumnTypeHelper
    {
        /// <summary>
        /// The column types, see http://www.sqlite.org/datatype3.html.
        /// 2.1 Determination Of Column Affinity
        /// 
        /// The affinity of a column is determined by the declared type of the column, according to the following rules in the order shown:
        /// 
        /// 1.If the declared type contains the string "INT" then it is assigned INTEGER affinity.
        /// 
        /// 2.If the declared type of the column contains any of the strings "CHAR", "CLOB", or "TEXT" then that column has TEXT affinity. 
        /// Notice that the type VARCHAR contains the string "CHAR" and is thus assigned TEXT affinity.
        /// 
        /// 3.If the declared type for a column contains the string "BLOB" or if no type is specified then the column has affinity NONE.
        /// 
        /// 4.If the declared type for a column contains any of the strings "REAL", "FLOA", or "DOUB" then the column has REAL affinity.
        /// 
        /// 5.Otherwise, the affinity is NUMERIC.
        /// 
        /// Note that the order of the rules for determining column affinity is important. 
        /// A column whose declared type is "CHARINT" will match both rules 1 and 2 
        /// but the first rule takes precedence and so the column affinity will be INTEGER.
        /// 
        /// </summary>
        private static IDictionary<Type, string> columnTypes = new Dictionary<Type, string>()
        {
            { typeof(int), "INT" },
            { typeof(uint), "INT" },
            { typeof(long), "INT" },
            { typeof(ulong), "INT" },
            { typeof(short), "INT" },
            { typeof(ushort), "INT" },
            { typeof(sbyte), "INT" },
            { typeof(byte), "INT" },
            { typeof(char), "CHAR" },
            { typeof(string), "TEXT" },
            { typeof(double), "DOUB" },
            { typeof(decimal), "DOUB" },
            { typeof(float), "FLOA" },
            { typeof(bool), "NUMERIC" },
            { typeof(DateTime), "TEXT" },
            { typeof(DateTimeOffset), "TEXT" },
            { typeof(Guid), "TEXT" },
        };

        /// <summary>
        /// Gets the CLR type from the column type.
        /// </summary>
        /// <param name="columnType">Type of the column.</param>
        /// <returns></returns>
        public static Type GetClrTypeForColumnType(string columnType)
        {
            int underscoreIndex = columnType.IndexOf('_');
            string type = string.Empty;
            if (columnType.Length > underscoreIndex)
            {
                type = columnType.Substring(underscoreIndex + 1);
            }
            //error when the type cannot be found
            return Type.GetType(type, true);
        }

        /// <summary>
        /// Gets the sqlite columntype from the CLR type.
        /// </summary>
        /// <param name="clrType">Type of the color.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Types is unknown for a mapping to a sqlite type.</exception>
        public static string GetColumnTypeForClrType(Type clrType)
        {
            string prefix;
            if(!columnTypes.TryGetValue(clrType, out prefix))
            {
                throw new InvalidOperationException("Types is unknown for a mapping to a sqlite type.");
            }
            return string.Format("{0}_{1}", prefix, clrType.ToString());
        }
    }
}
