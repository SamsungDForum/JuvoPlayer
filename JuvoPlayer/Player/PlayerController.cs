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
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Configuration;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Player
{
    public class PlayerController : IPlayerController
    {
        private bool seeking;
        private TimeSpan duration;

        private readonly IDrmManager drmManager;
        private readonly IPlayer player;
        private readonly Dictionary<StreamType, IPacketStream> streams = new Dictionary<StreamType, IPacketStream>();

        private readonly Subject<string> streamErrorSubject = new Subject<string>();
        private readonly Subject<bool> reconfigureSubject = new Subject<bool>();

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public PlayerController(IPlayer player, IDrmManager drmManager)
        {
            this.drmManager = drmManager ??
                              throw new ArgumentNullException(nameof(drmManager), "drmManager cannot be null");
            this.player = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");

            streams[StreamType.Audio] =
                new PacketStream(StreamType.Audio, this.player, drmManager, new AudioCodecExtraDataHandler());
            streams[StreamType.Video] =
                new PacketStream(StreamType.Video, this.player, drmManager, new VideoCodecExtraDataHandler());
        }

        public IObservable<int> BufferingProgress()
        {
            return player.BufferingProgress();
        }

        public IObservable<string> PlaybackError()
        {
            return player.PlaybackError().Merge(streamErrorSubject);
        }

        public IObservable<TimeSpan> TimeUpdated()
        {
            return player.PlayerClock().Sample(PlayerControllerConfig.TimeUpdatedInterval);
        }

        public IObservable<PlayerState> StateChanged()
        {
            return player.StateChanged();
        }

        public IObservable<TimeSpan> DataClock()
        {
            return player.DataClock();
        }

        public IObservable<TimeSpan> PlayerClock()
        {
            return player.PlayerClock();
        }

        public IObservable<bool> ConfigurationChanged(StreamType stream)
        {
            if (!streams.ContainsKey(stream))
                throw new ArgumentException("Unconfigured stream", nameof(stream));

            return streams[stream].ConfigurationChanged();
        }


        public void OnClipDurationChanged(TimeSpan duration)
        {
            this.duration = duration;
        }

        public void OnDRMInitDataFound(DRMInitData data)
        {
            if (!streams.ContainsKey(data.StreamType))
                return;

            streams[data.StreamType].OnDRMFound(data);
        }

        public void OnSetDrmConfiguration(DRMDescription description)
        {
            drmManager?.UpdateDrmConfiguration(description);
        }

        public void OnPause()
        {
            player.Pause();
        }

        public void OnPlay()
        {
            player.Play();
        }

        public async Task OnSeek(TimeSpan time)
        {
            if (seeking)
                throw new InvalidOperationException("Seek already in progress");

            try
            {
                // prevent simultaneously seeks
                seeking = true;

                if (time > duration)
                    time = duration;

                await player.Seek(time);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Operation Canceled");
            }
            finally
            {
                seeking = false;
            }
        }

        public void OnStop()
        {
            Logger.Info("");

            foreach (var stream in streams.Values)
                stream.OnClearStream();

            player.Stop();
        }

        public void OnSuspend()
        {
            player.Suspend();
        }

        public Task OnResume()
        {
            return player.Resume();
        }

        public void OnStreamConfigReady(StreamConfig config)
        {
            if (!streams.ContainsKey(config.StreamType()))
                return;

            streams[config.StreamType()].OnStreamConfigChanged(config);
        }

        public void OnPacketReady(Packet packet)
        {
            if (!streams.ContainsKey(packet.StreamType))
                return;

            streams[packet.StreamType].OnAppendPacket(packet);
        }

        public void OnStreamError(string errorMessage)
        {
            streamErrorSubject.OnNext(errorMessage);
        }

        public void OnSetPlaybackRate(float rate)
        {
            player.SetPlaybackRate(rate);
        }

        public IPlayerClient Client
        {
            get => player.Client;
            set => player.Client = value;
        }

        #region getters

        TimeSpan IPlayerController.ClipDuration => duration;

        #endregion

        public void Dispose()
        {
            Logger.Info("");
            // It is possible that streams waits for some events to complete
            // eg. drm initialization, and after unblock they will call disposed
            // player.
            // Remember to firstly dispose streams and later player
            foreach (var stream in streams.Values)
                stream.Dispose();

            player?.Dispose();
            streamErrorSubject.Dispose();
            reconfigureSubject.Dispose();
        }
    }
}