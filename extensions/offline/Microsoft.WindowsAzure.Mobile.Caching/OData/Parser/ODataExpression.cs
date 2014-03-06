using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public abstract class ODataExpression
    {
        public ExpressionType ExpressionType { get; internal set; }

        public abstract ODataExpression Accept(ODataExpressionVisitor visitor);
    }    

    public class ODataUnaryExpression : ODataExpression
    {
        public ODataUnaryExpression(ODataExpression expr, ExpressionType type)
        {
            this.Operand = expr;
            this.ExpressionType = type;
        }

        public ODataExpression Operand { get; private set; }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitUnary(this);
        }
    }

    public class ODataBinaryExpression : ODataExpression
    {
        public ODataBinaryExpression(ODataExpression left, ODataExpression right)
        {
            this.Left = left;
            this.Right = right;
        }

        public ODataExpression Left { get; private set; }
        public ODataExpression Right { get; private set; }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitBinary(this);
        }
    }

    public class ODataConstantExpression : ODataExpression
    {
        public ODataConstantExpression(object value)
        {
            this.Value = value;
            this.ExpressionType = ExpressionType.Constant;
        }

        public object Value { get; private set; }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitConstant(this);
        }
    }

    public class ODataMemberExpression : ODataExpression
    {
        public ODataMemberExpression(ODataExpression instance, string memberName)
        {
            if (string.IsNullOrEmpty(memberName))
            {
                throw new ArgumentNullException("memberName");
            }

            this.Instance = instance;
            this.Member = memberName;
            this.ExpressionType = ExpressionType.MemberAccess;
        }

        public ODataExpression Instance { get; private set; }
        public string Member { get; private set; }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitMember(this);
        }
    }

    public class ODataFunctionCallExpression : ODataExpression
    {
        public ODataFunctionCallExpression(string name, ODataExpression[] arguments)
        {
            this.Name = name;
            this.Arguments = arguments;
            this.ExpressionType = ExpressionType.Call;
        }

        public string Name { get; private set; }
        public ODataExpression[] Arguments { get; private set; }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitFunctionCall(this);
        }
    }

    public class ODataOrderByExpression : ODataExpression
    {
        public ODataOrderByExpression(IEnumerable<Selector> selectors)
        {
            if (selectors == null)
            {
                throw new ArgumentNullException("selectors");
            }

            this.Selectors = selectors;
            this.ExpressionType = (ExpressionType)10005;
        }

        public IEnumerable<Selector> Selectors { get; private set; }

        public struct Selector
        {
            public readonly ODataExpression Expression;
            public readonly Order Order;
            public Selector(ODataExpression e, Order order)
            {
                this.Expression = e;
                this.Order = order;
            }
        }
        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitOrderBy(this);
        }
    }

    public class ODataInlineCountExpression : ODataExpression
    {
        public ODataInlineCountExpression(InlineCount count)
        {
            if (!Enum.IsDefined(typeof(InlineCount), count))
            {
                throw new ArgumentException(string.Format("Undefined enum value on enum {0}.", count));
            }
            this.Value = count;
            this.ExpressionType = ExpressionType.Constant;
        }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitInlineCount(this);
        }

        public InlineCount Value { get; private set; }
    }

    public class ODataSkipExpression : ODataExpression
    {
        public ODataSkipExpression(int skip)
        {
            if (skip < 0)
            {
                throw new ArgumentException("skip must be > 0");
            }
            this.Value = skip;
            this.ExpressionType = ExpressionType.Constant;
        }

        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitSkip(this);
        }

        public int Value { get; private set; }
    }

    public class ODataTopExpression : ODataExpression
    {
        public ODataTopExpression(int top)
        {
            if (top < 0)
            {
                throw new ArgumentException("top must be > 0");
            }
            this.Value = top;
            this.ExpressionType = ExpressionType.Constant;
        }
        public override ODataExpression Accept(ODataExpressionVisitor visitor)
        {
            return visitor.VisitTop(this);
        }

        public int Value { get; private set; }
    }
}
