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

using JuvoPlayer.Common;
using System;

namespace JuvoPlayer.Demuxers
{
    public enum InitializationMode
    {
        // Stream has been already initialized so preparing StreamConfig is not needed
        Minimal,
        // Stream needs full initialization
        Full
    };

    public interface IDemuxer : IDisposable
    {
        void StartForExternalSource(InitializationMode initMode);
        void StartForUrl(string url);
        void ChangePID(int pid);
        void Reset();
        void Pause();
        void Resume();
        void Flush();
        bool IsPaused { get; }

        IObservable<TimeSpan> ClipDurationChanged();
        IObservable<DRMInitData> DRMInitDataFound();
        IObservable<StreamConfig> StreamConfigReady();
        IObservable<Packet> PacketReady();
        IObservable<string> DemuxerError();
    }
}