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
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer;
using JuvoPlayer.Common;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Tizen;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.Services;
using ILogger = JuvoLogger.ILogger;
using PlayerState = XamarinPlayer.Services.PlayerState;
using StreamDefinition = XamarinPlayer.Services.StreamDescription;
using StreamDescription = JuvoPlayer.Common.StreamDescription;

[assembly: Dependency(typeof(PlayerService))]

namespace XamarinPlayer.Tizen.Services
{
    sealed class PlayerService : JuvoPlayer.PlayerServiceProxy, XamarinPlayer.Services.IPlayerService, ISeekLogicClient
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public new PlayerState State => ToPlayerState(base.State);

        private SeekLogic _seekLogic = null; // needs to be initialized in constructor!

        public new TimeSpan CurrentPosition
        {
            get
            {
                if (_seekLogic.IsSeekAccumulationInProgress == false && _seekLogic.IsSeekInProgress == false)
                    _currentPosition = base.CurrentPosition;
                return _currentPosition;
            }
            set => _currentPosition = value;
        }
        private TimeSpan _currentPosition;

        public new TimeSpan Duration
        {
            get
            {
                _duration = base.Duration;
                return _duration;
            }
            set => _duration = value;
        }
        private TimeSpan _duration;

        public TimeSpan PlayerCurrentPosition => base.CurrentPosition;

        public TimeSpan PlayerDuration => base.Duration;

        public PlayerService()
            : base(new PlayerServiceImpl (((FormsApplication) Forms.Context).MainWindow))
        {
            _seekLogic = new SeekLogic(this);
        }

        public void ChangeActiveStream(StreamDefinition stream)
        {
            var streamDescription = new StreamDescription
            {
                Id = stream.Id,
                Description = stream.Description,
                StreamType = ToJuvoStreamType(stream.Type)
            };

            base.ChangeActiveStream(streamDescription);
        }

        public void DeactivateStream(StreamDefinition.StreamType streamType)
        {
            base.DeactivateStream(ToJuvoStreamType(streamType));
        }

        public List<StreamDefinition> GetStreamsDescription(StreamDefinition.StreamType streamType)
        {
            return base.GetStreamsDescription(ToJuvoStreamType(streamType))
                .Select(o => new StreamDefinition
                {
                    Id = o.Id,
                    Description = o.Description,
                    Default = o.Default,
                    Type = ToStreamType(o.StreamType)
                }).ToList();
        }

        public void SetSource(object o)
        {
            Logger.Info("");
            if (!(o is ClipDefinition))
                return;
            base.SetSource((ClipDefinition) o);
        }

        public new IObservable<PlayerState> StateChanged()
        {
            return base.StateChanged().Select(ToPlayerState);
        }

        private StreamType ToJuvoStreamType(StreamDefinition.StreamType streamType)
        {
            switch (streamType)
            {
                case StreamDefinition.StreamType.Audio:
                    return StreamType.Audio;
                case StreamDefinition.StreamType.Video:
                    return StreamType.Video;
                case StreamDefinition.StreamType.Subtitle:
                    return StreamType.Subtitle;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        private StreamDefinition.StreamType ToStreamType(StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return StreamDefinition.StreamType.Audio;
                case StreamType.Video:
                    return StreamDefinition.StreamType.Video;
                case StreamType.Subtitle:
                    return StreamDefinition.StreamType.Subtitle;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        private PlayerState ToPlayerState(JuvoPlayer.Common.PlayerState state)
        {
            switch (state)
            {
                case JuvoPlayer.Common.PlayerState.Idle:
                    return PlayerState.Idle;
                case JuvoPlayer.Common.PlayerState.Prepared:
                    return PlayerState.Prepared;
                case JuvoPlayer.Common.PlayerState.Paused:
                    return PlayerState.Paused;
                case JuvoPlayer.Common.PlayerState.Playing:
                    return PlayerState.Playing;
                default:
                    Logger.Error($"Unsupported state {state}");
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void Seek(TimeSpan to)
        {
            base.SeekTo(to);
        }

        public new Task SeekTo(TimeSpan to)
        {
            if(to < TimeSpan.Zero)
                _seekLogic.SeekBackward();
            else if(to > TimeSpan.Zero)
                _seekLogic.SeekForward();
            return Task.CompletedTask;
        }
    }
}
