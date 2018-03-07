using System;
using JuvoPlayer.Common;

namespace XamarinPlayer.Services
{
    public delegate void PlayerStateChangedEventHandler(object sender, PlayerStateChangedEventArgs e);
    public delegate void ShowSubtitleEventHandler(object sender, ShowSubtitleEventArgs e);

    public interface IPlayerService : IDisposable
    {
        event PlayerStateChangedEventHandler StateChanged;
        event ShowSubtitleEventHandler ShowSubtitle;

        TimeSpan Duration { get; }

        TimeSpan CurrentPosition { get; }

        bool IsSeekingSupported { get; }

        PlayerState State { get; }

        void SetSource(ClipDefinition clip);

        void Start();

        void Stop();

        void Pause();

        void SeekTo(TimeSpan position);
    }
}
