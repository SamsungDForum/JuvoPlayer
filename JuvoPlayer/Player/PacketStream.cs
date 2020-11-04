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
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using Nito.AsyncEx;

namespace JuvoPlayer.Player
{
    internal class PacketStream : IPacketStream
    {
        private readonly IDrmManager drmManager;
        private readonly IPlayer player;

        private ICdmInstance currentCdmInstance;

        private StreamConfig config;
        private readonly StreamType streamType;
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private bool forceDrmChange;
        private static readonly AsyncLock packetStreamLock = new AsyncLock();

        public PacketStream(StreamType streamType, IPlayer player, IDrmManager drmManager)
        {
            this.streamType = streamType;
            this.drmManager = drmManager ?? throw new ArgumentNullException(nameof(drmManager), "drmManager cannot be null");
            this.player = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");
        }

        public async Task OnAppendPacket(Packet packet)
        {
            if (packet.StreamType != streamType)
                throw new ArgumentException("packet type doesn't match");

            if (config == null)
                throw new InvalidOperationException("Packet stream is not configured");

            using (await packetStreamLock.LockAsync())
            {
                if (packet is EncryptedPacket encPacket)
                {
                    if (currentCdmInstance == null)
                    {
                        Logger.Error($"No matching CDM Instance for stream packet of type {encPacket.StreamType}!");
                        return;
                    }
                    encPacket.CdmInstance = currentCdmInstance;
                }
            }

            await player.AppendPacket(packet);
        }

        public void OnStreamConfigChanged(StreamConfig config)
        {
            Logger.Info($"{streamType}");

            if (config == null)
                throw new ArgumentNullException(nameof(config), "config cannot be null");

            if (config.StreamType() != streamType)
                throw new ArgumentException("config type doesn't match");

            if (config is BufferStreamConfig)
            {
                player.SetStreamConfig(config);
                return;
            }

            forceDrmChange = (this.config != null);

            this.config = config;

            player.SetStreamConfig(this.config);
        }

        public void OnClearStream()
        {
            Logger.Info($"{streamType}");

            currentCdmInstance = null;
            config = null;
        }

        public async Task OnDRMFound(DrmInitData data)
        {
            using (await packetStreamLock.LockAsync())
            {
                Logger.Info($"{streamType}");

                if (!forceDrmChange && currentCdmInstance != null)
                    return;

                IDrmSession newSession = await drmManager.GetDrmSession(data);

                // Do not reset wait for DRM event. If there is no valid session
                // do not want to append new data
                //
                if (newSession == null)
                    return;

                Logger.Info($"{streamType}: New DRM session found");
                forceDrmChange = false;

                // Set new session as current & let data submitters run wild.
                // There is no need to store sessions. They live in player queue
                currentCdmInstance = newSession.CdmInstance;
            }
        }

        public void Dispose()
        {
            OnClearStream();
        }
    }
}
