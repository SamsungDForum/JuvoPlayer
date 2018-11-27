// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using ElmSharp;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Utils;

namespace JuvoPlayer.Player.EsPlayer
{
    public class EsPlayer : IPlayer
    {
        private enum EsPlayerState
        {
            Stopped,
            Playing,
            Paused
        }

        private static readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private EsPlayerPacketStorage packetStorage;
        private EsStreamController streamControl;

        private EsPlayerState playerState;

        private readonly CompositeDisposable subscriptions;

        public EsPlayer()
            : this(WindowUtils.CreateElmSharpWindow())
        {
        }

        public EsPlayer(Window window)
        {
            try
            {
                packetStorage = new EsPlayerPacketStorage();
                packetStorage.Initialize(StreamType.Audio);
                packetStorage.Initialize(StreamType.Video);

                streamControl = new EsStreamController(packetStorage, window);
                streamControl.Initialize(StreamType.Audio);
                streamControl.Initialize(StreamType.Video);

                subscriptions = new CompositeDisposable
                {
                    streamControl.PlayerInitialized().Subscribe(unit => playerState = EsPlayerState.Playing,
                        SynchronizationContext.Current),
                    streamControl.PlaybackCompleted().Subscribe(unit => playerState = EsPlayerState.Stopped,
                        SynchronizationContext.Current),
                    streamControl.ErrorOccured().Subscribe(msg => playerState = EsPlayerState.Stopped,
                        SynchronizationContext.Current)
                };

                // Initialize player state
                playerState = EsPlayerState.Stopped;

            }
            catch (InvalidOperationException ioe)
            {
                logger.Error("EsPlayer failure: " + ioe.Message);
                throw ioe;
            }
        }

        #region IPlayer Interface Implementation
        public void AppendPacket(Packet packet)
        {
            packetStorage.AddPacket(packet);
        }

        public void Pause()
        {
            logger.Info("");

            switch (playerState)
            {
                case EsPlayerState.Playing:
                    streamControl.Pause();
                    break;
                default:
                    logger.Warn($"Player not playing. Current State {playerState}");
                    return;
            }

            playerState = EsPlayerState.Paused;
        }

        public void Play()
        {
            logger.Info("");

            switch (playerState)
            {
                case EsPlayerState.Stopped:
                    streamControl.Play();
                    break;
                case EsPlayerState.Paused:
                    streamControl.Resume();
                    break;
                default:
                    logger.Warn($"Player not stopped/paused. Current State {playerState}");
                    return;
            }

            playerState = EsPlayerState.Playing;
        }

        public void Stop()
        {
            logger.Info("");

            switch (playerState)
            {
                case EsPlayerState.Playing:
                case EsPlayerState.Paused:
                    streamControl.Stop();
                    break;
                default:
                    logger.Warn($"Player not playing/paused. Current State {playerState}");
                    return;
            }

            playerState = EsPlayerState.Stopped;
        }

        public void Seek(TimeSpan time)
        {
            logger.Info("");
            streamControl.Seek(time);
        }

        public void SetDuration(TimeSpan duration)
        {
            logger.Info("");
            throw new NotImplementedException();
        }

        public void SetPlaybackRate(float rate)
        {
            logger.Info("");
            throw new NotImplementedException();
        }

        public void SetStreamConfig(StreamConfig config)
        {
            logger.Info(config.StreamType().ToString());

            var configPacket = BufferConfigurationPacket.Create(config);

            streamControl.SetStreamConfiguration(configPacket);
        }

        #region IPlayer Interface event callbacks

        public IObservable<string> PlaybackError()
        {
            return streamControl.ErrorOccured();
        }

        public IObservable<Unit> Initialized()
        {
            return streamControl.PlayerInitialized();
        }

        public IObservable<TimeSpan> TimeUpdated()
        {
            return streamControl.TimeUpdated();
        }

        public IObservable<Unit> Paused()
        {
            return streamControl.Paused();
        }

        public IObservable<Unit> Played()
        {
            return streamControl.Played();
        }

        public IObservable<SeekArgs> SeekStarted()
        {
            return streamControl.SeekStarted();
        }

        public IObservable<Unit> SeekCompleted()
        {
            return streamControl.SeekCompleted();
        }

        public IObservable<Unit> Stopped()
        {
            return streamControl.Stopped();
        }

        public IObservable<Unit> PlaybackCompleted()
        {
            return streamControl.PlaybackCompleted();
        }

        #endregion
        #endregion

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Detach event handlers
                    logger.Info("Detach event handlers");
                    subscriptions.Dispose();

                    // Clean packet storage and stream controller
                    logger.Info("StreamController and PacketStorage shutdown");
                    streamControl.Dispose();
                    packetStorage.Dispose();
                }

                disposedValue = true;
            }
        }

        ~EsPlayer()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
