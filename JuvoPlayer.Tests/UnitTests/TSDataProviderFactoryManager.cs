/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
        [Category("Negative")]
        public static void RegisterDataProviderFactory_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => manager.RegisterDataProviderFactory(null));
        }

        [Test]
        [Category("Positive")]
        public static void RegisterDataProviderFactory_OK()
        {
            var factory = Substitute.For<IDataProviderFactory>();
            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
        }

        [Test]
        [Category("Negative")]
        public static void RegisterDataProviderFactory_Unsupported()
        {
            var factory = Substitute.For<IDataProviderFactory>();
            Assert.DoesNotThrow(() => manager.RegisterDataProviderFactory(factory));
            Assert.Throws<ArgumentException>(() => manager.RegisterDataProviderFactory(factory));
        }

        [Test]
        [Category("Negative")]
        public static void CreateDataProvider_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => manager.CreateDataProvider(null));
        }

        [Test]
        [Category("Negative")]
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
        [Category("Positive")]
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
