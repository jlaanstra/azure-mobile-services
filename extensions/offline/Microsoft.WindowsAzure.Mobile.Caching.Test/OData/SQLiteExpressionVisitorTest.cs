using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Microsoft.WindowsAzure.Mobile.Caching.Test.OData
{
    [TestClass]
    public class SQLiteExpressionVisitorTest
    {
        [TestMethod]
        public void SQLiteExpressionTest()
        {
            IQueryOptions query = new StaticQueryOptions()
            {
                Filter = new FilterQuery("Price gt -3.45 and InStock eq True"),
                OrderBy = new OrderByQuery("OtherId"),
                Skip = new SkipQuery("2"),
                Top = new TopQuery("15"),
                InlineCount = new InlineCountQuery("allpages"),
            };

            SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();

            visitor.VisitQueryOptions(query);

            Assert.AreEqual("SELECT * FROM {0} WHERE ((Price > -3.45) AND (InStock == 1)) ORDER BY OtherId ASCENDING LIMIT 15 OFFSET 2", visitor.SqlStatement);
        }

        [TestMethod]
        public void AdvancedSQLiteExpressionTest()
        {
            IQueryOptions query = new StaticQueryOptions()
            {
                Filter = new FilterQuery("Price le 3.45E-2 and OtherId ge 45 and not InStock or startswith(Name, 'P') and substring(Name, 3) eq 'duct'"),
                OrderBy = new OrderByQuery("OtherId desc"),
                Skip = new SkipQuery("10"),
                Top = new TopQuery("15"),
                InlineCount = new InlineCountQuery("none"),
            };

            SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();

            visitor.VisitQueryOptions(query);

            Assert.AreEqual("SELECT * FROM {0} WHERE ((((Price <= 0.0345) AND (OtherId >= 45)) AND NOT InStock) OR ((Name LIKE 'P') AND ((substr(Name, 3)) == 'duct'))) ORDER BY OtherId DESCENDING LIMIT 15 OFFSET 10", visitor.SqlStatement);
        }

        [TestMethod]
        public void SQLiteExpressionTest2()
        {
            IQueryOptions query = new StaticQueryOptions()
            {
                Filter = new FilterQuery("Price ne -INF and Price ne -INFf or Added = datetime'2014-03-04T23:20:28Z' and Available lt time'17:03:00'"),
                OrderBy = new OrderByQuery("OtherId desc"),
                Skip = new SkipQuery("10"),
                Top = new TopQuery("15"),
                InlineCount = new InlineCountQuery("none"),
            };

            SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();

            visitor.VisitQueryOptions(query);

            Assert.AreEqual("SELECT * FROM {0} WHERE (((Price != -Infinity) AND (Price != -Infinity)) OR ((Added == datetime('2014-03-04T23:20:28.000Z')) AND (Available < time('17:03:00')))) ORDER BY OtherId DESCENDING LIMIT 15 OFFSET 10", visitor.SqlStatement);
        }

        [TestMethod]
        public void SQLiteExpressionTest3()
        {
            IQueryOptions query = new StaticQueryOptions()
            {
                Filter = new FilterQuery("Price ne -3.4D and Price ne -INFF and Price ne -10 and Price eq -Price"),
                OrderBy = new OrderByQuery("OtherId desc"),
                Skip = new SkipQuery("10"),
                Top = new TopQuery("15"),
                InlineCount = new InlineCountQuery("none"),
            };

            SQLiteExpressionVisitor visitor = new SQLiteExpressionVisitor();

            visitor.VisitQueryOptions(query);

            Assert.AreEqual("SELECT * FROM {0} WHERE ((((Price != -3.4) AND (Price != -Infinity)) AND (Price != -10)) AND (Price == -Price)) ORDER BY OtherId DESCENDING LIMIT 15 OFFSET 10", visitor.SqlStatement);
        }
    }
}
