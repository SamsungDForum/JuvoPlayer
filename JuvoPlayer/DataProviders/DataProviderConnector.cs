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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Player;
using JuvoPlayer.Common;

namespace JuvoPlayer.DataProviders
{
    public class DataProviderConnector : IDisposable
    {
        private class PlayerClient : IPlayerClient
        {
            private readonly IDataProvider dataProvider;
            private readonly DataProviderConnector connector;

            public PlayerClient(DataProviderConnector owner, IDataProvider provider)
            {
                connector = owner;
                dataProvider = provider;
            }

            public async Task<TimeSpan> Seek(TimeSpan position, CancellationToken token)
            {
                try
                {
                    connector.Disconnect();
                    return await dataProvider.Seek(position, token);
                }
                finally
                {
                    connector.Connect();
                }
            }

            public async Task<TimeSpan> ChangeRepresentation(TimeSpan position, object representation, CancellationToken token)
            {
                var stream = representation as StreamDescription;
                if (stream == null)
                    throw new ArgumentException($"Argument is not of type {typeof(StreamDescription)}", nameof(representation));

                try
                {
                    connector.Disconnect();
                    dataProvider.Pause();
                    dataProvider.ChangeActiveStream(stream);
                    return await dataProvider.Seek(position, token);
                }
                finally
                {
                    connector.Connect();
                }
            }
        }

        private CompositeDisposable fixedSubscriptions;
        private CompositeDisposable reconnectableSubscriptions;

        private IPlayerClient client;
        private readonly IPlayerController playerController;
        private readonly IDataProvider dataProvider;
        private readonly SynchronizationContext context;

        public DataProviderConnector(IPlayerController playerController, IDataProvider dataProvider,
            SynchronizationContext context = null)
        {
            this.playerController = playerController ?? throw new ArgumentNullException(nameof(playerController), "Player controller cannot be null");
            this.dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider), "Data provider cannot be null");
            this.context = context ?? SynchronizationContext.Current;

            SetupConnector();
        }

        private void SetupConnector()
        {
            reconnectableSubscriptions = new CompositeDisposable();

            // reconnectables
            Connect();

            fixedSubscriptions = new CompositeDisposable
            { 
                // Data provider subscriptions
                playerController.StateChanged().Subscribe(dataProvider.OnStateChanged, context),
                playerController.DataClock().Subscribe(dataProvider.OnDataClock, context),

                // Player controller subscriptions
                dataProvider.ClipDurationChanged().Subscribe(playerController.OnClipDurationChanged, context),
                dataProvider.StreamError().Subscribe(playerController.OnStreamError, context)
            };

            InstallPlayerClient();
        }

        private void Connect()
        {
            reconnectableSubscriptions.Add(playerController.TimeUpdated().Subscribe(dataProvider.OnTimeUpdated, context));
            reconnectableSubscriptions.Add(dataProvider.DRMInitDataFound().Subscribe(async data => await playerController.OnDrmInitDataFound(data), context));

            reconnectableSubscriptions.Add(dataProvider.PacketReady().Subscribe(async packet => await playerController.OnPacketReady(packet), context));
            reconnectableSubscriptions.Add(dataProvider.SetDrmConfiguration().Subscribe(async description => await playerController.OnSetDrmConfiguration(description), context));
            reconnectableSubscriptions.Add(dataProvider.StreamConfigReady().Subscribe(playerController.OnStreamConfigReady, context));
        }

        private void Disconnect()
        {
            reconnectableSubscriptions.Clear();
        }

        private void InstallPlayerClient()
        {
            client = new PlayerClient(this, dataProvider);
            playerController.Client = client;
        }

        public void Dispose()
        {
            reconnectableSubscriptions.Dispose();
            fixedSubscriptions.Dispose();
        }
    }
}
