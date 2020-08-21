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
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;

namespace JuvoPlayer.Drms
{
    internal sealed class DecryptedEMEPacket : Packet
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly AsyncContextThread releaseThread;
        public HandleSize HandleSize { get; set; }
        public DecryptedEMEPacket(AsyncContextThread releaseThread)
        {
            this.releaseThread = releaseThread ?? throw new ArgumentNullException(nameof(releaseThread), "releaseThread cannot be null");
        }

        public void CleanHandle()
        {
            HandleSize = new HandleSize();
        }

        private void ReleaseUnmanagedResources()
        {
            if (HandleSize.handle != 0)
            {
                releaseThread.Factory.Run(() =>
                {
                    try
                    {
                        API.ReleaseHandle(HandleSize);
                        CleanHandle();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                });
            }
        }

        #region Disposable Support

        private bool IsDisposed { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                ReleaseUnmanagedResources();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }

        ~DecryptedEMEPacket()
        {
            Dispose(false);
        }
        #endregion
    }
}
