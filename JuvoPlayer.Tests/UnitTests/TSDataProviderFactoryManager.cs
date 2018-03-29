using System;
using JuvoPlayer.Common;
using JuvoPlayer.DataProviders;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    [Description("")]
    class DataProviderFactoryManagerTests
    {
        private static DataProviderFactoryManager manager;
        [SetUp]
        public static void Init()
        {
            manager = new DataProviderFactoryManager();
        }

        [TearDown]
        public static void Destroy()
        {
            manager = null;
        }

        [Test]
        [Description("RegisterDataProviderFactory throws on null argument")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.RegisterDataProviderFactory M")]
        //[Property("COVPARAM", " ")]
        public static void RegisterDataProviderFactory_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => manager.RegisterDataProviderFactory(null));
        }

        [Test]
        [Description("RegisterDataProviderFactory succeeds")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.RegisterDataProviderFactory M")]
        //[Property("COVPARAM", " ")]
        public static void RegisterDataProviderFactory_OK()
        {
            var factory = Substitute.For<IDataProviderFactory>();
            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
        }

        [Test]
        [Description("RegisterDataProviderFactory throws on not supported clip")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.RegisterDataProviderFactory M")]
        //[Property("COVPARAM", " ")]
        public static void RegisterDataProviderFactory_Unsupported()
        {
            var factory = Substitute.For<IDataProviderFactory>();
            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
            Assert.Throws<ArgumentException>(() => manager.RegisterDataProviderFactory(factory));
        }

        [Test]
        [Description("CreateDataProvider throws on null argument")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.CreateDataProvider M")]
        //[Property("COVPARAM", " ")]
        public static void CreateDataProvider_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => manager.CreateDataProvider(null));
        }

        [Test]
        [Description("CreateDataProvider throws on not supported clip")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.CreateDataProvider M")]
        //[Property("COVPARAM", " ")]
        public static void CreateDataProvider_NotSupported()
        {
            var factory = Substitute.For<IDataProviderFactory>();
            var clip = new ClipDefinition
            {
                Type = "FAKE"
            };

            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
            Assert.Throws<ArgumentException>(() => manager.CreateDataProvider(clip));
        }

        [Test]
        [Description("CreateDataProvider created factory for proper clip")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.CreateDataProvider M")]
        //[Property("COVPARAM", " ")]
        public static void CreateDataProvider_OK()
        {
            var factory = Substitute.For<IDataProviderFactory>();
            var provider = Substitute.For<IDataProvider>();
            var clip = new ClipDefinition
            {
                Type = "TEST"
            };
            factory.SupportsClip(Arg.Is(clip)).Returns(true);
            factory.Create(Arg.Is(clip)).Returns(provider);

            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
            var receivedProvider = manager.CreateDataProvider(clip);
            Assert.That(receivedProvider, Is.EqualTo(provider));
        }
    }
}
