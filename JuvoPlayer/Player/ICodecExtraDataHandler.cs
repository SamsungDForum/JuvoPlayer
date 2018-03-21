using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Player
{
    internal interface ICodecExtraDataHandler
    {
        void OnAppendPacket(Packet packet);
        void OnStreamConfigChanged(StreamConfig config);
    }
}
