using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;
using System;
using System.Threading.Tasks;

namespace XamarinPlayer.Services
{
    public delegate void PlayerStateChangedEventHandler(object sender, PlayerStateChangedEventArgs e);

    public interface IPlayerService : IDisposable
    {
        event PlayerStateChangedEventHandler StateChanged;

        event PlaybackCompleted PlaybackCompleted;
        event ShowSubtitile ShowSubtitle;

        int Duration { get; }

        int CurrentPosition { get; }

        PlayerState State { get; }

        void SetSource(ClipDefinition clip);

        void Start();

        void Stop();

        void Pause();

        void SeekTo(int positionMs);
    }
}
