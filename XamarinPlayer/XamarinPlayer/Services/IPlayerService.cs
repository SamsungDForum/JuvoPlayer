using System;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;

namespace XamarinPlayer.Services
{
    public delegate void PlayerStateChangedEventHandler(object sender, PlayerStateChangedEventArgs e);

    public interface IPlayerService : IDisposable
    {
        event PlayerStateChangedEventHandler StateChanged;

        event PlaybackCompleted PlaybackCompleted;
        event ShowSubtitile ShowSubtitle;

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
