/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElmSharp;
using JuvoPlayer.Common;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

namespace JuvoPlayer
{
    public class PlayerServiceProxy<T> : IPlayerService where T : IPlayerService, new()
    {
        private readonly AsyncContextThread playerThread;
        private IPlayerService proxied;

        public PlayerServiceProxy()
        {
            playerThread = new AsyncContextThread();

            // Create proxy object from within playerThread. This should assure valid 
            // SynchronizationContext.Current, separate of caller SynchronizationContext.
            // proxied is "set twice" to keep playerThread and caller with current proxied object
            proxied = playerThread.Factory.StartNew(() =>
            {
                proxied = new T();
                return proxied;
            }).WaitAndUnwrapException();
        }

        public void SetWindow(Window window)
        {
            playerThread.Factory.StartNew(() => proxied.SetWindow(window));
        }
        public TimeSpan Duration => proxied.Duration;

        public TimeSpan CurrentPosition => proxied.CurrentPosition;

        public bool IsSeekingSupported => proxied.IsSeekingSupported;

        public PlayerState State => proxied.State;

        public string CurrentCueText => proxied.CurrentCueText;

        public Task Pause()
        {
            return playerThread.Factory.StartNew(async () => await proxied.Pause()).Unwrap();
        }

        public Task SeekTo(TimeSpan time)
        {
            return playerThread.Factory.StartNew(async () => await proxied.SeekTo(time)).Unwrap();
        }

        public void ChangeActiveStream(StreamDescription streamDescription)
        {
            playerThread.Factory.StartNew(() => proxied.ChangeActiveStream(streamDescription));
        }

        public void DeactivateStream(StreamType streamType)
        {
            playerThread.Factory.StartNew(() => proxied.DeactivateStream(streamType));
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return playerThread.Factory.StartNew(() => proxied.GetStreamsDescription(streamType)).Result;
        }

        public void SetSource(ClipDefinition clip)
        {
            playerThread.Factory.StartNew(() => proxied.SetSource(clip));
        }

        public Task Start()
        {
            return playerThread.Factory.StartNew(async () => await proxied.Start()).Unwrap();
        }

        public void Stop()
        {
            playerThread.Factory.StartNew(() => proxied.Stop());
        }

        public Task Suspend()
        {
            return playerThread.Factory.StartNew(async () => await proxied.Suspend()).Unwrap();
        }

        public Task Resume()
        {
            return playerThread.Factory.StartNew(async () => await proxied.Resume()).Unwrap();
        }

        public IObservable<PlayerState> StateChanged()
        {
            return proxied.StateChanged();
        }

        public IObservable<string> PlaybackError()
        {
            return proxied.PlaybackError();
        }

        public IObservable<int> BufferingProgress()
        {
            return proxied.BufferingProgress();
        }

        public IObservable<TimeSpan> PlayerClock()
        {
            return proxied.PlayerClock();
        }

        public void Dispose()
        {
            playerThread.Factory.StartNew(() => { proxied.Dispose(); });
            playerThread.Join();
            playerThread.Dispose();
        }
    }
}