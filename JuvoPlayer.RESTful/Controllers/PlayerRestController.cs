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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using JuvoPlayer.Platforms.Tizen;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Formatting = Newtonsoft.Json.Formatting;

namespace JuvoPlayer.RESTful.Controllers
{
    public class PlayerRestController : WebApiController
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer.RESTful");
        private static IPlayer _dashPlayer;
        private static AsyncContextThread _playerThread;

        public PlayerRestController()
        {
            _playerThread = new AsyncContextThread();
        }

        [Route(HttpVerbs.Get, "/")]
        public string Home()
        {
            return "Player is running";
        }

        [Route(HttpVerbs.Post, "/")]
        public async Task<string> HandlePlayerRequest([FormData] NameValueCollection request)
        {
            var response = new Dictionary<string, string>();
            try
            {
                var order = request["order"];
#pragma warning disable CS1998 // This async method lacks 'await' operators
                switch (order)
                {
                    case "play":
                        var uri = request["uri"];
                        var clip = new Clip
                        {
                            MpdUri = uri,
                        };
                        if (request.ContainsKey("drm"))
                        {
                            clip.DrmDescription = JsonConvert.DeserializeObject<DrmDescription>(request["drm"]);
                            _logger.Info("Successfully obtained drm description.");
                        }

                        await RunOnPlayerThread(async () =>
                        {
                            if (_dashPlayer != null)
                            {
                                await _dashPlayer.DisposeAsync();
                                _dashPlayer = null;
                            }

                            _dashPlayer = BuildPlayer(clip);
                            await _dashPlayer.Prepare();
                            _dashPlayer.Play();

                            response["duration"] = _dashPlayer.Duration.ToString();
                        });
                        break;
                    case "stop":
                        await RunOnPlayerThread(async () =>
                        {
                            await _dashPlayer.DisposeAsync();
                            _dashPlayer = null;
                        });
                        break;
                    case "pause":
                        await RunOnPlayerThread(async () => { await _dashPlayer.Pause(); });
                        break;
                    case "resume":
                        await RunOnPlayerThread(async () => { _dashPlayer.Play(); });
                        break;
                    case "seek":
                        int.TryParse(request["destination"], out var seekDestination);
                        await RunOnPlayerThread(async () =>
                        {
                            await _dashPlayer.Seek(TimeSpan.FromSeconds(seekDestination));

                            response["position"] = _dashPlayer.Position.ToString();
                        });
                        break;
                    case "changeVideo":
                        int.TryParse(request["index"], out var newVideoIndex);
                        await RunOnPlayerThread(async () =>
                        {
                            await ChangeStream(ContentType.Video, newVideoIndex);
                            response["current"] = JsonConvert.SerializeObject(GetCurrentStreamInfo(ContentType.Video));
                        });
                        break;
                    case "changeAudio":
                        int.TryParse(request["index"], out var newAudioIndex);
                        await RunOnPlayerThread(async () =>
                        {
                            await ChangeStream(ContentType.Audio, newAudioIndex);
                            response["current"] = JsonConvert.SerializeObject(GetCurrentStreamInfo(ContentType.Audio));
                        });
                        break;
                    case "getStreamsInfo":
                        await RunOnPlayerThread(async () =>
                        {
                            response["streams"] = JsonConvert.SerializeObject(_dashPlayer.GetStreamGroups());
                        });
                        break;
                    case "position":
                        await RunOnPlayerThread(async () =>
                        {
                            response["position"] = _dashPlayer.Position.ToString();
                        });
                        break;
                    case "state":
                        await RunOnPlayerThread(async () => { response["state"] = _dashPlayer.State.ToString(); });
                        break;
                }
#pragma warning restore CS1998
            }
            catch (Exception e)
            {
                response["error"] = e.Message + e.StackTrace;
            }

            return JsonConvert.SerializeObject(response, Formatting.Indented);
        }

        private Task RunOnPlayerThread(Func<Task> workToDo)
        {
            return _playerThread.Factory.StartNew(async () => { await workToDo(); })
                .Unwrap();
        }

        public struct Clip
        {
            public string MpdUri { get; set; }
            public DrmDescription? DrmDescription { get; set; }

            public override string ToString()
            {
                return MpdUri;
            }
        }

        public struct DrmDescription
        {
            public string KeySystem { get; set; }
            public string LicenseServerUri { get; set; }
            public Dictionary<string, string> RequestHeaders { get; set; }
        }

        private Format GetCurrentStreamInfo(ContentType contentType)
        {
            Format info = null;
            var (streamGroups, streamSelectors) = _dashPlayer.GetSelectedStreamGroups();
            for (int i = 0; i < streamGroups.Length; i++)
            {
                if (streamGroups[i].ContentType == contentType)
                {
                    info = streamSelectors[i] != null
                        ? streamGroups[i].Streams[streamSelectors[i].Select(streamGroups[i])].Format
                        : streamGroups[i].Streams.FirstOrDefault()?.Format;
                }
            }

            return info;
        }

        private async Task ChangeStream(ContentType contentType, int index)
        {
            var (streamGroups, streamSelectors) = _dashPlayer.GetSelectedStreamGroups();

            var groupIndex = Array.FindIndex(streamGroups, group => group.ContentType == contentType);
            streamSelectors[groupIndex] = new FixedStreamSelector(index);

            await _dashPlayer.SetStreamGroups(streamGroups, streamSelectors);
        }

        private static IPlayer BuildPlayer(Clip clip, Configuration configuration = default)
        {
            var window = new ElmSharpWindow(AppContext.Instance.MainWindow);
            var mpdUri = clip.MpdUri;
            var drmInfo = clip.DrmDescription;
            var builder = new DashPlayerBuilder();
            builder = builder
                .SetWindow(window)
                .SetMpdUri(mpdUri)
                .SetConfiguration(configuration);
            if (drmInfo != null)
            {
                var keySystem = drmInfo.Value.KeySystem;
                var licenseServerUri = drmInfo.Value.LicenseServerUri;
                var requestHeaders = drmInfo.Value.RequestHeaders;
                builder = builder
                    .SetKeySystem(keySystem)
                    .SetDrmSessionHandler(new YoutubeDrmSessionHandler(
                        licenseServerUri,
                        requestHeaders));
            }

            return builder.Build();
        }
    }
}