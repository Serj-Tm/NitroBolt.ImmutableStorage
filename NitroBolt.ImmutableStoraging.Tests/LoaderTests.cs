using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroBolt.QSharp;

namespace NitroBolt.ImmutableStoraging.Tests
{
    [TestClass]
    public class LoaderTests
    {
        [TestMethod]
        public void A1Saving()
        {
            var q1 = new QNode("a1", new [] { new QNode("i", new [] { new QNode("12")})});

            var serializer = new QSerializer(new Dictionary<Type, string>(), new Dictionary<Type, Dictionary<string, string>>(),
                new Dictionary<Type, Dictionary<string, PushInfo[]>>());
            var qnode = serializer.Save(new A1(12));
            var qtext = qnode.ToText();
            Assert.AreEqual("q: I: 12", qtext);
        }

        [TestMethod]
        public void A1Loading()
        {
            var q1 = new QNode("q", new[] { new QNode("I", new[] { new QNode("12") }) });

            var serializer = new QSerializer(new Dictionary<Type, string>(), new Dictionary<Type, Dictionary<string, string>>(),
                new Dictionary<Type, Dictionary<string, PushInfo[]>>());
            var a1 = (A1)serializer.Load(typeof(A1), q1, new Dictionary<string, Dictionary<object, object>>());
            Assert.AreEqual(12, a1.I);
        }

        public readonly QNodeBuilder q = null;

    }

    public class A1
    {
        public A1(int i)
        {
            this.I = i;
        }
        public readonly int I;
    }
}