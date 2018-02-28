using System;
using JuvoPlayer.Common.Logging;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;

namespace JuvoPlayer.Common
{
    public unsafe class DecryptedEMEPacket : StreamPacket
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

        public override void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~DecryptedEMEPacket()
        {
            ReleaseUnmanagedResources();
        }
    }
}
