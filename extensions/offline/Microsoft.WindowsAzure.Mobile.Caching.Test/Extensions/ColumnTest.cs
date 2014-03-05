using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Microsoft.WindowsAzure.Mobile.Caching.Test.Extensions
{
    [TestClass]
    public class ColumnTest
    {
        [TestMethod]
        public void ColumnToStringTest()
        {
            Column col = new Column("test", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)), true);

            Assert.AreEqual("Name: test, DataType: TEXT_System.String, Nullable: True, DefaultValue: , PrimaryKeyIndex: 0", col.ToString());
        }

        [TestMethod]
        public void AsSQLTest()
        {
            Column col = new Column("test", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)).ToString(), true);

            Assert.AreEqual("\"test\" \"TEXT_System.String\"", col.AsSQL());
        }

        [TestMethod]
        public void AsSQLNotNullableTest()
        {
            Column col = new Column("test", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)).ToString(), false);

            Assert.AreEqual("\"test\" \"TEXT_System.String\" NOT NULL", col.AsSQL());
        }

        [TestMethod]
        public void AsSQLPrimaryKeyTest()
        {
            Column col = new Column("test", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)).ToString(), false, null, 1);

            Assert.AreEqual("\"test\" \"TEXT_System.String\" PRIMARY KEY NOT NULL", col.AsSQL());
        }

        [TestMethod]
        public void AsSQLDefaultValueTest()
        {
            Column col = new Column("test", ColumnTypeHelper.GetColumnTypeForClrType(typeof(string)).ToString(), false, 1);

            Assert.AreEqual("\"test\" \"TEXT_System.String\" NOT NULL DEFAULT 1", col.AsSQL());
        }
    }
}
