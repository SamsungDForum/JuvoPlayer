using NUnit.Framework;
using JuvoPlayer;
using System;
using JuvoPlayer.Common;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    [Description("")]
    class DataProviderFactoryManagerTests
    {
        private class FakeDataProvider : IDataProvider
        {
            public event ClipDurationChanged ClipDurationChanged;
            public event DRMInitDataFound DRMInitDataFound;
            public event SetDrmConfiguration SetDrmConfiguration;
            public event StreamConfigReady StreamConfigReady;
            public event StreamPacketReady StreamPacketReady;
            public event StreamsFound StreamsFound;

            public void OnChangeRepresentation(int representationId)
            {
                throw new NotImplementedException();
            }

            public void OnPaused()
            {
                throw new NotImplementedException();
            }

            public void OnPlayed()
            {
                throw new NotImplementedException();
            }

            public void OnSeek(TimeSpan time)
            {
                throw new NotImplementedException();
            }

            public void OnStopped()
            {
                throw new NotImplementedException();
            }

            public void OnTimeUpdated(TimeSpan time)
            {
                throw new NotImplementedException();
            }

            public void Start()
            {
                throw new NotImplementedException();
            }
        }

        private class FakeDataProviderFactory : IDataProviderFactory
        {
            public IDataProvider Create(ClipDefinition clip)
            {
                return new FakeDataProvider();
            }

            public bool SupportsClip(ClipDefinition clip)
            {
                return clip.Type == "TEST";
            }
        }

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
            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(new FakeDataProviderFactory()));
        }

        [Test]
        [Description("RegisterDataProviderFactory throws on not supported clip")]
        [Property("SPEC", "JuvoPlayer.DataProviderFactoryManager.RegisterDataProviderFactory M")]
        //[Property("COVPARAM", " ")]
        public static void RegisterDataProviderFactory_Unsupported()
        {
            var factory = new FakeDataProviderFactory();
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
            var factory = new FakeDataProviderFactory();
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
            var factory = new FakeDataProviderFactory();
            var clip = new ClipDefinition
            {
                Type = "TEST"
            };

            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
            Assert.IsNotNull(manager.CreateDataProvider(clip));
            Assert.IsInstanceOf<FakeDataProvider>(manager.CreateDataProvider(clip));
        }
    }
}
