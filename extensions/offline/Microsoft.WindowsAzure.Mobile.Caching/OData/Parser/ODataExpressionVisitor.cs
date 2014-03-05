using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public abstract class ODataExpressionVisitor
    {
        public abstract IQueryOptions VisitQueryOptions(IQueryOptions query);

        public virtual ODataExpression Visit(ODataExpression expr)
        {
            return expr.Accept(this);
        }

        public virtual ODataExpression VisitBinary(ODataBinaryExpression expr)
        {
            ODataExpression left = null;
            ODataExpression right = null;

            if (expr.Left != null)
            {
                left = this.Visit(expr.Left);
            }
            if (expr.Right != null)
            {
                right = this.Visit(expr.Right);
            }
            if (left != expr.Left || right != expr.Right)
            {
                return new ODataBinaryExpression(left, right);
            }

            return expr;
        }

        public virtual ODataExpression VisitUnary(ODataUnaryExpression expr)
        {
            ODataExpression operand = this.Visit(expr.Operand);
            if (operand != expr.Operand)
            {
                return new ODataUnaryExpression(operand, expr.ExpressionType);
            }
            return expr;
        }

        public virtual ODataExpression VisitConstant(ODataConstantExpression expr)
        {
            return expr;
        }

        public virtual ODataExpression VisitMember(ODataMemberExpression expr)
        {
            return expr;
        }

        public virtual ODataExpression VisitFunctionCall(ODataFunctionCallExpression expr)
        {
            bool updated = false;

            ODataExpression[] args = new ODataExpression[expr.Arguments.Length];
            for (int i = 0; i < expr.Arguments.Length; i++)
            {
                ODataExpression arg = expr.Arguments[i];
                ODataExpression newArg = this.Visit(arg);
                if (newArg != arg)
                {
                    updated = true;
                    args[i] = newArg;
                }
                else
                {
                    args[i] = arg;
                }
            }
            if (updated)
            {
                return new ODataFunctionCallExpression(expr.Name, args);
            }
            return expr;
        }

        public virtual ODataExpression VisitOrderBy(ODataOrderByExpression expr)
        {
            foreach (ODataOrderByExpression.Selector s in expr.Selectors)
            {
                this.Visit(s.Expression);
            }
            return expr;
        }

        public virtual ODataExpression VisitTop(ODataTopExpression expr)
        {
            return expr;
        }

        public virtual ODataExpression VisitSkip(ODataSkipExpression expr)
        {
            return expr;
        }

        public virtual ODataExpression VisitInlineCount(ODataInlineCountExpression expr)
        {
            return expr;
        }
    }
}
