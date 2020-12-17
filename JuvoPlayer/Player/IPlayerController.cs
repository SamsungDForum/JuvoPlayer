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
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player
{
    public interface IPlayerController : IDisposable
    {
        #region ui_slots
        void OnPause();
        void OnPlay();
        Task OnSeek(TimeSpan time);
        void OnSetPlaybackRate(float rate);
        void OnStop();
        void OnSuspend();
        Task OnResume();
        Task OnRepresentationChanged(object representation);

        #endregion

        #region data_provider_slots
        void OnClipDurationChanged(TimeSpan duration);
        Task OnDrmInitDataFound(DrmInitData data);
        Task OnSetDrmConfiguration(DrmDescription description);
        void OnStreamConfigReady(StreamConfig config);
        Task OnPacketReady(Packet packet);
        void OnStreamError(string errorMessage);
        IPlayerClient Client { set; }
        #endregion

        #region getters

        TimeSpan ClipDuration { get; }

        #endregion

        IObservable<int> BufferingProgress();
        IObservable<string> PlaybackError();
        IObservable<TimeSpan> TimeUpdated();
        IObservable<PlayerState> StateChanged();
        IObservable<TimeSpan> DataClock();
    }
}
