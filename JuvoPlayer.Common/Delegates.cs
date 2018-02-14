using System;

namespace JuvoPlayer.Common.Delegates
{
    public delegate void PlaybackCompleted();
    public delegate void PlaybackError(string message);
    public delegate void PlayerInitialized();
    public delegate void ShowSubtitile(Subtitle subtitle);
    public delegate void TimeUpdated(TimeSpan time);
}
