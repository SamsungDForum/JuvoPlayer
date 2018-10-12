using System;
using JuvoLogger;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;

namespace JuvoPlayer.Common
{
    internal unsafe sealed class DecryptedEMEPacket : Packet
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
                        Logger.Error("Error: " + e.Message);
                    }
                });
            }
        }

        #region Disposable Support
        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            ReleaseUnmanagedResources();

            IsDisposed = true;
        }

        ~DecryptedEMEPacket()
        {
            Dispose(false);
        }
        #endregion
    }
}
