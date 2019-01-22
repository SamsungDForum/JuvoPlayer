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
using System.Reactive.Disposables;
using System.Threading;
using JuvoPlayer.Player;
using JuvoPlayer.Common;

namespace JuvoPlayer.DataProviders
{
    public class DataProviderConnector : IDisposable
    {
        private CompositeDisposable subscriptions;

        public DataProviderConnector(IPlayerController playerController, IDataProvider dataProvider,
            SynchronizationContext context = null)
        {
            Connect(playerController, dataProvider, context);
        }

        private void Connect(IPlayerController controller, IDataProvider dataProvider,
            SynchronizationContext context)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller), "Player controller cannot be null");

            if (dataProvider == null)
                return;

            if (context == null)
                context = SynchronizationContext.Current;

            subscriptions = new CompositeDisposable
            {
                dataProvider.ClipDurationChanged()
                    .Subscribe(controller.OnClipDurationChanged, context),
                dataProvider.DRMInitDataFound()
                    .Subscribe(controller.OnDRMInitDataFound, context),
                dataProvider.SetDrmConfiguration()
                    .Subscribe(controller.OnSetDrmConfiguration, context),
                dataProvider.StreamConfigReady()
                    .Subscribe(controller.OnStreamConfigReady, context),
                dataProvider.PacketReady()
                    .Subscribe(controller.OnPacketReady, context),
                dataProvider.BufferingStarted()
                    .Subscribe(unit => controller.OnBufferingStarted(), context),
                dataProvider.BufferingCompleted()
                    .Subscribe(unit => controller.OnBufferingCompleted(), context),
                dataProvider.StreamError()
                    .Subscribe(controller.OnStreamError, context),
                controller.TimeUpdated().Subscribe(dataProvider.OnTimeUpdated, context),
                controller.StateChanged().Subscribe(dataProvider.OnStateChanged, context),
                controller.SeekStarted()
                    .Subscribe(args => dataProvider.OnSeekStarted(args.Position, args.Id), context),
                controller.SeekCompleted()
                    .Subscribe(unit => dataProvider.OnSeekCompleted(), context),
            };
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
        }
    }
}