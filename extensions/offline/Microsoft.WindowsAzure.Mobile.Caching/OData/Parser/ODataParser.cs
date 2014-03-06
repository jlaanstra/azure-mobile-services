using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    /// <summary>
    /// Based on http://odata.codeplex.com/SourceControl/latest#ODataLib/OData/Dev10/Microsoft/Data/OData/Query/ExpressionLexer.cs and other classes from the project.
    /// </summary>
    public class ODataParser
    {
        public static ODataExpression ParseSkip(string skip)
        {
            int intValue;
            if (int.TryParse(skip, out intValue))
            {
                return ODataExpressionFactory.Skip(intValue);
            }
            else
            {
                throw new InvalidOperationException("Skip can only take an integer");
            }
        }

        public static ODataExpression ParseTop(string top)
        {
            int intValue;
            if (int.TryParse(top, out intValue))
            {
                return ODataExpressionFactory.Top(intValue);
            }
            else
            {
                throw new InvalidOperationException("Top can only take an integer");
            }
        }

        public static ODataExpression ParseOrderBy(string orderby)
        {
            return ODataExpressionFactory.OrderBy(orderby.Split(',').Select(o => o.Trim()).Select(o => ToSelector(o)));
        }

        private static ODataOrderByExpression.Selector ToSelector(string orderby)
        {
            string[] orderParts = orderby.Split(' ');
            string propertyName = orderParts.First();
            Order order = (orderParts.Length > 1) ? (orderParts[1].Equals("desc") ? Order.Descending : Order.Ascending) : Order.Ascending;
            return new ODataOrderByExpression.Selector(ODataExpressionFactory.Member(null, propertyName), order);
        }

        public static ODataExpression ParseInlineCount(string inlineCount)
        {
            InlineCount enumValue;
            if (Enum.TryParse(inlineCount, true, out enumValue))
            {
                return ODataExpressionFactory.InlineCount(enumValue);
            }
            else
            {
                throw new InvalidOperationException("InlineCount did not match enum value.");
            }
        }

        public static ODataExpression ParseFilter(string filter)
        {
            return new ODataFilterParser(filter).Parse();
        }
    }
}
