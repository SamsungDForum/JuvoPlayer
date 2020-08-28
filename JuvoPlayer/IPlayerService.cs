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
using System.Threading.Tasks;
using ElmSharp;
using JuvoPlayer.Common;

namespace JuvoPlayer
{
    public interface IPlayerService : IDisposable
    {
        TimeSpan Duration { get; }
        TimeSpan CurrentPosition { get; }
        bool IsSeekingSupported { get; }
        PlayerState State { get; }
        string CurrentCueText { get; }
        void Pause();
        Task SeekTo(TimeSpan to);
        Task ChangeActiveStream(StreamDescription streamDescription);
        void DeactivateStream(StreamType streamType);
        List<StreamDescription> GetStreamsDescription(StreamType streamType);
        Task SetSource(ClipDefinition clip);
        void Start();
        void Stop();
        void Suspend();
        Task Resume();
        IObservable<PlayerState> StateChanged();
        IObservable<string> PlaybackError();
        IObservable<int> BufferingProgress();
        IObservable<TimeSpan> PlayerClock();
        void SetWindow(Window window);
    }
}