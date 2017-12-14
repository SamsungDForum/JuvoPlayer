using System;
using System.Threading.Tasks;

namespace XamarinMediaPlayer.Services
{
    public delegate void PlayerStateChangedEventHandler(object sender, PlayerStateChangedEventArgs e);

    public interface IPlayerService : IDisposable
    {
        event PlayerStateChangedEventHandler StateChanged;
        event EventHandler PlaybackCompleted;

        int Duration { get; }

        int CurrentPosition { get; }

        PlayerState State { get; }

        void SetSource(string uri);

        Task PrepareAsync();

        void Start();

        void Stop();

        void Pause();

        void SeekTo(int positionMs);
    }
}
