using System;

namespace JuvoPlayer.Common
{
    public delegate void ClipDurationChanged(TimeSpan clipDuration);
    public delegate void DRMInitDataFound(DRMInitData data);
    public delegate void PacketReady(Packet packet);
    public delegate void PlaybackCompleted();
    public delegate void PlaybackError(string message);
    public delegate void PlayerInitialized();
    public delegate void ShowSubtitile(Subtitle subtitle);
    public delegate void StreamConfigReady(StreamConfig config);
    public delegate void TimeUpdated(TimeSpan time);
}
