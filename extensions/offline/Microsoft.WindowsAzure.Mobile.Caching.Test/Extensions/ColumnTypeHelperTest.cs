using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Microsoft.WindowsAzure.Mobile.Caching.Test.Extensions
{
    [TestClass]
    public class ColumnTypeHelperTest
    {
        [TestMethod]
        public void ColumnTypeHelperThrowsForInvalidType()
        {
            Exception e = null;
            try
            {
                ColumnTypeHelper.GetColumnTypeForClrType(typeof(Buffer));
            }
            catch(Exception ex)
            {
                e = ex;
            }

            Assert.IsNotNull(e);
        }
    }
}
