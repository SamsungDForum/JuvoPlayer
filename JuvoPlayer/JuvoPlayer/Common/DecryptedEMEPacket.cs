using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tizen.TV.Security.DrmDecrypt;

namespace JuvoPlayer.Common
{
    public unsafe class DecryptedEMEPacket : StreamPacket
    {
        private AsyncContextThread releaseThread;
        public HandleSize HandleSize { get; set; }
        public DecryptedEMEPacket(AsyncContextThread releaseThread)
        {
            this.releaseThread = releaseThread ?? throw new ArgumentNullException("releaseThread cannot be null");
        }

        public void CleanHandle()
        {
            if (HandleSize.handle != 0)
            {
                releaseThread.Factory.Run(() =>
                {
                    try
                    {
                        API.ReleaseHandle(HandleSize);
                    }
                    catch (Exception e)
                    {
                        Tizen.Log.Error("JuvoPlayer", "Error: " + e.Message);
                    }
                });
            }
        }
    }
}
