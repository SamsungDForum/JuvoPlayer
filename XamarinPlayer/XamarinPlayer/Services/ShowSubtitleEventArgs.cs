using System;

namespace XamarinPlayer.Services
{
    public class ShowSubtitleEventArgs : EventArgs
    {
        public ShowSubtitleEventArgs(Subtitle subtitle)
        {
            Subtitle = subtitle;
        }
        public Subtitle Subtitle { get; }
    }
}
