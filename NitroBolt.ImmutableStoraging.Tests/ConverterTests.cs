using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NitroBolt.ImmutableStoraging.Tests
{
    [TestClass]
    public class ConverterTests
    {
        [TestMethod]
        public void IntConverting()
        {
            Assert.AreEqual(12, Converter.ToInt("12"));
        }
        [TestMethod]
        public void LongConverting()
        {
            Assert.AreEqual(12, Converter.ToLong("12"));
        }
    }
}
