/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using EmbedIO;
using EmbedIO.WebApi;
using JuvoLogger;
using JuvoPlayer.RESTful.Controllers;

namespace JuvoPlayer.RESTful
{
    public class HttpPlayer : IDisposable
    {
        private WebServer _server;

        public HttpPlayer(int port)
        {
            try
            {
                var url = $"http://*:{port}";
                _server = CreateWebServer(url);
                _server.RunAsync();
                Log.Info("Listening on port: " + port);
            }
            catch (Exception e)
            {
                Log.Info(e.Message);
                Log.Info(e.StackTrace);
            }
        }

        private WebServer CreateWebServer(string url)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithWebApi("/player", m => m
                    .WithController<PlayerRestController>());

            server.StateChanged += (s, e) => Log.Info($"WebServer New State - {e.NewState}");

            return server;
        }

        public void Dispose()
        {
            _server.Dispose();
        }
    }
}