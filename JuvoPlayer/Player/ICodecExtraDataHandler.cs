using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Player
{
    public interface ICodecExtraDataHandler
    {
        void OnAppendPacket(Packet packet);
        void OnStreamConfigChanged(StreamConfig config);
    }
}
