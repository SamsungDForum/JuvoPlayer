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
using JuvoPlayer.Common;
using Nito.AsyncEx;

namespace JuvoPlayer
{
    public class PlayerServiceProxy : IPlayerService
    {
        private readonly AsyncContextThread playerThread;
        private readonly IPlayerService proxied;

        public PlayerServiceProxy(IPlayerService proxied)
        {
            this.proxied = proxied;
            playerThread = new AsyncContextThread();
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

        public Task ChangeActiveStream(StreamDescription streamDescription)
        {
            return playerThread.Factory.StartNew(async () => await proxied.ChangeActiveStream(streamDescription));
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