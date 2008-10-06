using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;

#if !NUNIT
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
using NUnit.Framework;
using TestClass = NUnit.Framework.TestFixtureAttribute;
using TestInitialize = NUnit.Framework.SetUpAttribute;
using TestCleanup = NUnit.Framework.TearDownAttribute;
using TestMethod = NUnit.Framework.TestAttribute;
#endif 

namespace Csla.Test.Serialization
{
    [TestClass()]
    public class SerializationTests
    {

        [Serializable]
        private class TestBusinessListBaseCollection : BusinessListBase<TestBusinessListBaseCollection, TestIndexableItem>
        {
            public TestBusinessListBaseCollection(int sampleSize)
                : this(sampleSize, 100, true) { }
            public TestBusinessListBaseCollection(int sampleSize, int sparsenessFactor, bool randomize)
            {
                Random rnd = new Random();
                for (int i = 0; i < sampleSize; i++)
                {
                    int nextRnd;
                    if (randomize)
                        nextRnd = rnd.Next(sampleSize / sparsenessFactor);
                    else
                        nextRnd = i / sparsenessFactor;
                    Add(
                      new TestIndexableItem
                      {
                          IndexedString = nextRnd.ToString(),
                          IndexedInt = nextRnd,
                          NonIndexedString = nextRnd.ToString()
                      }
                      );
                }
            }
        }

        
        [Serializable]
        private class TestCollection : BusinessListBase<TestCollection, TestItem>
        {
        }

        [Serializable]
        private class TestItem : BusinessBase<TestItem>
        {
            protected override object GetIdValue()
            {
                return 0;
            }

            public TestItem()
            {
                MarkAsChild();
            }
        }

        [Serializable]
        private class TestIndexableItem : BusinessBase<TestIndexableItem>, Csla.Core.IReadOnlyObject
        {
            [Indexable]
            public string IndexedString { get; set; }
            [Indexable]
            public int IndexedInt { get; set; }
            public string NonIndexedString { get; set; }


            #region IReadOnlyObject Members

            bool Csla.Core.IReadOnlyObject.CanReadProperty(string propertyName)
            {
                throw new NotImplementedException();
            }

            #endregion
        }

        [TestMethod()]
        public void TestIndexAfterSerialization()
        {
            var sampleSize = 100000;
            Console.WriteLine("Creating " + sampleSize + " element collection...");
            var blbCollection = new TestBusinessListBaseCollection(sampleSize);
            Console.WriteLine("Collection established.");

            //first query establishes the index
            var controlSet = blbCollection.ToList();

            var primeQuery = from i in blbCollection where i.IndexedString == "42" select i;
            var forcedPrimeITeration = primeQuery.ToArray();

            Stopwatch watch = new Stopwatch();
            //clone a collection - this will force a serialization/unserialization cycle
            var clonedCollection = blbCollection.Clone();
            //prime the clone, which will build the index since the cloned collection will lack one
            //  initially
            var primeClone = from i in clonedCollection where i.IndexedString == "42" select i;

            watch.Start();
            var indexedQuery = from i in clonedCollection where i.IndexedString == "42" select i;
            var forcedIterationIndexed = indexedQuery.ToArray();
            watch.Stop();

            var indexedRead = watch.ElapsedMilliseconds;

            watch.Reset();

            watch.Start();
            var nonIndexedQuery = from i in clonedCollection where i.NonIndexedString == "42" select i;
            var forcedIterationNonIndexed = nonIndexedQuery.ToArray();
            watch.Stop();

            var nonIndexedRead = watch.ElapsedMilliseconds;

            watch.Reset();

            watch.Start();
            var controlQuery = from i in controlSet where i.IndexedString == "42" select i;
            var forcedControlIteration = controlQuery.ToArray();
            watch.Stop();

            var controlRead = watch.ElapsedMilliseconds;


            Console.WriteLine("Sample size = " + sampleSize);
            Console.WriteLine("Indexed Read = " + indexedRead + "ms");
            Console.WriteLine("Non-Indexed Read = " + nonIndexedRead + "ms");
            Console.WriteLine("Standard Linq-to-objects Read = " + controlRead + "ms");
            Assert.IsTrue(indexedRead < nonIndexedRead);
            Assert.IsTrue(forcedIterationIndexed.Count() == forcedIterationNonIndexed.Count());
        }
        [TestMethod()]
        public void TestWithoutSerializableHandler()
        {
            Csla.ApplicationContext.GlobalContext.Clear();
            SerializationRoot root = new SerializationRoot();
            nonSerializableEventHandler handler = new nonSerializableEventHandler();
            handler.Reg(root);
            root.Data = "something";
            Assert.AreEqual(1, Csla.ApplicationContext.GlobalContext["PropertyChangedFiredCount"]);
            root.Data = "something else";
            Assert.AreEqual(2, Csla.ApplicationContext.GlobalContext["PropertyChangedFiredCount"]);

            //serialize an object with eventhandling objects that are nonserializable
            root = root.Clone();
            root.Data = "something new";

            //still at 2 even though we changed the property again 
            //when the clone method performs serialization, the nonserializable 
            //object containing an event handler for the propertyChanged event
            //is lost
            Assert.AreEqual(2, Csla.ApplicationContext.GlobalContext["PropertyChangedFiredCount"]);
        }

        [TestMethod()]
        public void Clone( )
        {
            Csla.ApplicationContext.GlobalContext.Clear( );
            SerializationRoot root = new SerializationRoot( );

            root = (SerializationRoot)root.Clone( );

            Assert.AreEqual(true, Csla.ApplicationContext.GlobalContext["Deserialized"], 
                "Deserialized not called");
        }

        [TestMethod()]
        public void SerializableEvents( )
        {
            Csla.ApplicationContext.GlobalContext.Clear( );

            SerializationRoot root = new SerializationRoot( );
            TestEventSink handler = new TestEventSink( );

            root.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler
                (OnIsDirtyChanged);

            root.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler
                (StaticOnIsDirtyChanged);

            root.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler
                (PublicStaticOnIsDirtyChanged);

            root.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler
                (OnIsDirtyChanged);  //will call this method twice since it is assigned twice

            handler.Reg(root);

            root.Data = "abc";
            
            Assert.AreEqual("abc", root.Data, "Data value not set");
            
            Assert.AreEqual("OnIsDirtyChanged", 
                Csla.ApplicationContext.GlobalContext["OnIsDirtyChanged"],
                "Didn't call local handler");
            
            Assert.AreEqual("StaticOnIsDirtyChanged",
                Csla.ApplicationContext.GlobalContext["StaticOnIsDirtyChanged"], 
                "Didn't call static handler");

            Assert.AreEqual("PublicStaticOnIsDirtyChanged",
                Csla.ApplicationContext.GlobalContext["PublicStaticOnIsDirtyChanged"],
                "Didn't call public static handler");

            Assert.AreEqual("Test.OnIsDirtyChanged",
                Csla.ApplicationContext.GlobalContext["Test.OnIsDirtyChanged"],
                "Didn't call serializable handler");
            
            Assert.AreEqual("Test.PrivateOnIsDirtyChanged",
                Csla.ApplicationContext.GlobalContext["Test.PrivateOnIsDirtyChanged"],
                "Didn't call serializable private handler");

            root = (SerializationRoot)root.Clone( );

            Csla.ApplicationContext.GlobalContext.Clear( );

            root.Data = "xyz";

            Assert.AreEqual("xyz", root.Data, "Data value not set");

            Assert.AreEqual(null, Csla.ApplicationContext.GlobalContext["OnIsDirtyChanged"],
                "Called local handler after clone");

            Assert.AreEqual(null, Csla.ApplicationContext.GlobalContext["StaticOnIsDirtyChanged"],
                "Called static handler after clone");

            Assert.AreEqual("PublicStaticOnIsDirtyChanged",
                Csla.ApplicationContext.GlobalContext["PublicStaticOnIsDirtyChanged"],
                "Didn't call public static handler after clone");

            Assert.AreEqual("Test.OnIsDirtyChanged",
                Csla.ApplicationContext.GlobalContext["Test.OnIsDirtyChanged"],
                "Didn't call serializable handler after clone");

            Assert.AreEqual(null, Csla.ApplicationContext.GlobalContext["Test.PrivateOnIsDirtyChanged"],
                "Called serializable private handler after clone");
        }

        [TestMethod()]
        public void TestValidationRulesAfterSerialization()
        {
            Csla.Test.ValidationRules.HasRulesManager root = Csla.Test.ValidationRules.HasRulesManager.NewHasRulesManager();
            root.Name = "";
            Assert.AreEqual(false, root.IsValid, "root should not start valid");

            root = root.Clone();
            Assert.AreEqual(false, root.IsValid, "root should not be valid after clone");
            root.Name = "something";
            Assert.AreEqual(true, root.IsValid, "root should be valid after property set");
            root = root.Clone();
            Assert.AreEqual(true, root.IsValid, "root should be valid after second clone");
        }

        [TestMethod()]
        public void TestAuthorizationRulesAfterSerialization()
        {
            Csla.Test.Security.PermissionsRoot root = Csla.Test.Security.PermissionsRoot.NewPermissionsRoot();

            try
            {
                root.FirstName = "something";
                Assert.Fail("Exception didn't occur");
            }
            catch (System.Security.SecurityException ex)
            {
                Assert.AreEqual("Property set not allowed", ex.Message);
            }

            Csla.Test.Security.TestPrincipal.SimulateLogin();

            try
            {
                root.FirstName = "something";
            }
            catch (System.Security.SecurityException ex)
            {
                Assert.Fail("exception occurred");
            }

            Csla.Test.Security.TestPrincipal.SimulateLogout();

            Csla.Test.Security.PermissionsRoot rootClone = root.Clone();

            try
            {
                rootClone.FirstName = "something else";
                Assert.Fail("Exception didn't occur");
            }
            catch (System.Security.SecurityException ex)
            {
                Assert.AreEqual("Property set not allowed", ex.Message);
            }

            Csla.Test.Security.TestPrincipal.SimulateLogin();

            try
            {
                rootClone.FirstName = "something new";
            }
            catch (System.Security.SecurityException ex)
            {
                Assert.Fail("exception occurred");
            }

            Csla.Test.Security.TestPrincipal.SimulateLogout();

        }

        private void OnIsDirtyChanged(object sender, 
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            Csla.ApplicationContext.GlobalContext["OnIsDirtyChanged"] = "OnIsDirtyChanged";
        }

        private static void StaticOnIsDirtyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            Csla.ApplicationContext.GlobalContext["StaticOnIsDirtyChanged"] = 
                "StaticOnIsDirtyChanged";
        }

        public static void PublicStaticOnIsDirtyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            Csla.ApplicationContext.GlobalContext["PublicStaticOnIsDirtyChanged"] = 
                "PublicStaticOnIsDirtyChanged";
        }

      [TestMethod]
      public void DCClone()
      {
        System.Configuration.ConfigurationManager.AppSettings["CslaSerializationFormatter"] = 
          "NetDataContractSerializer";
        Assert.AreEqual(
          Csla.ApplicationContext.SerializationFormatters.NetDataContractSerializer, 
          Csla.ApplicationContext.SerializationFormatter,
          "Formatter should be NetDataContractSerializer");

        DCRoot root = new DCRoot();
        root.Data = 123;
        DCRoot clone = root.Clone();

        Assert.IsFalse(ReferenceEquals(root,clone), "Object instance should be different");
        Assert.AreEqual(root.Data, clone.Data, "Data should match");
        Assert.IsTrue(root.IsDirty, "Root IsDirty should be true");
        Assert.IsTrue(clone.IsDirty, "Clone IsDirty should be true");
      }

      [TestMethod]
      public void DCEditLevels()
      {
        System.Configuration.ConfigurationManager.AppSettings["CslaSerializationFormatter"] =
          "NetDataContractSerializer";
        Assert.AreEqual(
          Csla.ApplicationContext.SerializationFormatters.NetDataContractSerializer,
          Csla.ApplicationContext.SerializationFormatter,
          "Formatter should be NetDataContractSerializer");

        DCRoot root = new DCRoot();
        root.BeginEdit();
        root.Data = 123;
        root.CancelEdit();

        Assert.AreEqual(0, root.Data, "Data should be 0");

        root.BeginEdit();
        root.Data = 123;
        root.ApplyEdit();

        Assert.AreEqual(123, root.Data, "Data should be 123");
      }
    
    }
}