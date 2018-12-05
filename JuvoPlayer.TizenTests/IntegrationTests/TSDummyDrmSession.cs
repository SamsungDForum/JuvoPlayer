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

﻿using System;
using System.Text;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms.DummyDrm;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSDummyDrmSession
    {
        private LoggerManager savedLoggerManager;
        private byte[] data = new byte[] {
                0xa0, 0xa1, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xab, 0xac, 0xad, 0xae, 0xaf,
                0xb0, 0xb1, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xbb, 0xbc, 0xbd, 0xbe, 0xbf,
                0xc0, 0xc1, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xcb, 0xcc, 0xcd, 0xce, 0xcf,
                0xd0, 0xd1, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xdb, 0xdc, 0xdd, 0xde, 0xdf  };

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            savedLoggerManager = LoggerManager.ResetForTests();
            LoggerManager.Configure("JuvoPlayer=Verbose", CreateLoggerFunc);
        }

        private static LoggerBase CreateLoggerFunc(string channel, LogLevel level)
        {
            return new ConsoleLogger(channel, level);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LoggerManager.RestoreForTests(savedLoggerManager);
        }

        public EncryptedPacket CreateDummyEncryptedPacket()
        {
            return new EncryptedPacket
            {
                Data = data,
                Dts = TimeSpan.FromSeconds(1),
                Pts = TimeSpan.FromSeconds(1),
                IsEOS = false,
                IsKeyFrame = true,
                StreamType = StreamType.Video,
            };
        }

        [Test]
        public async Task DecryptPacket_WhenPacketIsValid_DecryptsSuccessfully()
        {
            using (var drmSession = DummyDrmSession.Create())
            {
                await drmSession.Initialize();

                var encrypted = CreateDummyEncryptedPacket();

                using (var decrypted = await drmSession.DecryptPacket(encrypted))
                {
                    Assert.That(decrypted, Is.Not.Null);
                    Assert.That(decrypted, Is.InstanceOf<DecryptedEMEPacket>());

                    var decryptedEme = decrypted as DecryptedEMEPacket;

                    Assert.That(decryptedEme.Dts, Is.EqualTo(encrypted.Dts));
                    Assert.That(decryptedEme.Pts, Is.EqualTo(encrypted.Pts));
                    Assert.That(decryptedEme.IsEOS, Is.EqualTo(encrypted.IsEOS));
                    Assert.That(decryptedEme.IsKeyFrame, Is.EqualTo(encrypted.IsKeyFrame));
                    Assert.That(decryptedEme.StreamType, Is.EqualTo(encrypted.StreamType));
                    Assert.That(decryptedEme.HandleSize, Is.Not.Null);
                    Assert.That(decryptedEme.HandleSize.handle, Is.GreaterThan(0));
                    Assert.That(decryptedEme.HandleSize.size, Is.EqualTo(data.Length));
                }
            }
        }
    }
}
