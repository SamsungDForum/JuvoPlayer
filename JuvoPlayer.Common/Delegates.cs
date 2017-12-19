using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Common.Delegates
{
    public delegate void PlaybackCompleted();
    public delegate void ShowSubtitile(Subtitle subtitle);
    public delegate void TimeUpdated(double time);
}
