using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class ODataExpressionFactory
    {
        private ODataExpressionFactory() { }

        public static ODataExpression Equal(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.Equal;
            return bex;
        }

        public static ODataExpression NotEqual(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.NotEqual;
            return bex;
        }

        public static ODataExpression GreaterThan(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.GreaterThan;
            return bex;
        }

        public static ODataExpression GreaterThanOrEqual(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.GreaterThanOrEqual;
            return bex;
        }

        public static ODataExpression LessThan(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.LessThan;
            return bex;
        }

        public static ODataExpression LessThanOrEqual(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.LessThanOrEqual;
            return bex;
        }

        public static ODataExpression Add(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.Add;
            return bex;
        }

        public static ODataExpression Subtract(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.Subtract;
            return bex;
        }

        public static ODataExpression OrElse(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.OrElse;
            return bex;
        }

        public static ODataExpression AndAlso(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.AndAlso;
            return bex;
        }

        public static ODataExpression Multiply(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.Multiply;
            return bex;
        }

        public static ODataExpression Divide(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.Divide;
            return bex;
        }

        public static ODataExpression Modulo(ODataExpression left, ODataExpression right)
        {
            ODataBinaryExpression bex = new ODataBinaryExpression(left, right);
            bex.ExpressionType = ExpressionType.Modulo;
            return bex;
        }

        public static ODataExpression Negate(ODataExpression expr)
        {
            return new ODataUnaryExpression(expr, ExpressionType.Negate);
        }

        public static ODataExpression Not(ODataExpression expr)
        {
            return new ODataUnaryExpression(expr, ExpressionType.Not);
        }

        public static ODataExpression Constant(object value)
        {
            return new ODataConstantExpression(value);
        }

        public static ODataExpression Skip(int skip)
        {
            return new ODataSkipExpression(skip);
        }

        public static ODataExpression Top(int top)
        {
            return new ODataTopExpression(top);
        }

        public static ODataExpression Member(ODataExpression expr, string memberName)
        {
            return new ODataMemberExpression(expr, memberName);
        }

        public static ODataExpression FunctionCall(string name, ODataExpression[] arguments)
        {
            return new ODataFunctionCallExpression(name, arguments);
        }

        public static ODataExpression InlineCount(InlineCount count)
        {
            return new ODataInlineCountExpression(count);
        }

        public static ODataExpression OrderBy(IEnumerable<ODataOrderByExpression.Selector> selectors)
        {
            return new ODataOrderByExpression(selectors);
        }
    }
}
