using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM.Cenc
{
    class CencSession : IDRMSession
    {
        public string CurrentDrmScheme => throw new NotImplementedException();

        public StreamPacket DecryptPacket(StreamPacket packet)
        {
            throw new NotImplementedException();
        }

        public void SetDrmConfiguration(DRMDescription drmDescription)
        {
            throw new NotImplementedException();
        }

        public void UpdateSession(DRMInitData drmInitData)
        {
            throw new NotImplementedException();
        }
    }
}
