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
using System.IO;
using System.Net;
using System.Net.Http;
using JuvoPlayer.Utils;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSResourceLoader
    {
        [Test]
        [Category("Positive")]
        public void LoadAsNetworkResource_UrlIsValid_LoadsResource()
        {
            var httpMessageHandlerStub = Substitute.ForPartsOf<FakeHttpMessageHandler>();
            httpMessageHandlerStub.Send(Arg.Any<HttpRequestMessage>()).Returns(new HttpResponseMessage() { StatusCode = HttpStatusCode.OK, Content = new StringContent("Response")});
            var resourceLoader = CreateResourceLoader(new HttpClient(httpMessageHandlerStub));

            var receivedStream = resourceLoader.LoadAsNetworkResource(new Uri("http://valid_uri"));
            using (var reader = new StreamReader(receivedStream))
            {
                Assert.That(reader.ReadLine(), Is.EqualTo("Response"));
            }
        }

        [Test]
        [Category("Positive")]
        public void LoadAsEmbeddedAssembly_AssemblyContainsExceptedResource_LoadsResource()
        {
            var resourceName = "resource";
            var resourceContents = "resource contents";

            var assemblyStub = Substitute.For<System.Reflection.Assembly>();
            assemblyStub.GetManifestResourceNames().Returns(new[] {resourceName});

            using (var memoryStream = CreateMemoryStream(resourceContents))
            {
                assemblyStub.GetManifestResourceStream(Arg.Is(resourceName)).Returns(memoryStream);
                var resourceLoader = Substitute.ForPartsOf<ResourceLoader>();
                resourceLoader.FindAssembly(Arg.Is(resourceName)).Returns(assemblyStub);

                var receivedStream = resourceLoader.LoadAsEmbeddedResource(resourceName);

                var reader = new StreamReader(receivedStream);
                Assert.That(reader.ReadLine(), Is.EqualTo(resourceContents));
            }
        }

        [TestCase("http://valid_http_uri")]
        [TestCase("https://valid_https_uri")]
        [Category("Positive")]
        public void Load_PathPointsToNetworkResource_LoadsAsNetworkResource(string url)
        {
            var resourceLoader = Substitute.ForPartsOf<ResourceLoader>();
            var memoryStream = CreateMemoryStream("");
            resourceLoader.LoadAsNetworkResource(Arg.Any<Uri>()).Returns(memoryStream);

            var receivedStream = resourceLoader.Load(url);

            Assert.That(receivedStream, Is.EqualTo(memoryStream));
        }

        [Test]
        [Category("Positive")]
        public void Load_PathDoesntPointToNetworkResource_LoadsAsEmbeddedResource()
        {
            var resourceLoader = Substitute.ForPartsOf<ResourceLoader>();
            var memoryStream = CreateMemoryStream("");
            resourceLoader.LoadAsEmbeddedResource(Arg.Any<string>()).Returns(memoryStream);

            var receivedStream = resourceLoader.Load("embedded_resource");

            Assert.That(receivedStream, Is.EqualTo(memoryStream));
        }

        private static ResourceLoader CreateResourceLoader(HttpClient client)
        {
            var resourceLoader = new ResourceLoader();
            if (client != null)
                resourceLoader.HttpClient = client;
            return resourceLoader;
        }

        private Stream CreateMemoryStream(string contents)
        {
            var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            writer.WriteLine(contents);
            writer.Flush();
            memoryStream.Position = 0;
            return memoryStream;

        }
    }
}
