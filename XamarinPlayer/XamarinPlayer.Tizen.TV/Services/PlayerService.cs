using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Tizen;
using XamarinMediaPlayer.Services;
using XamarinMediaPlayer.Tizen.Services;
using Multimedia = Tizen.Multimedia;

[assembly: Dependency(typeof(PlayerService))]
namespace XamarinMediaPlayer.Tizen.Services
{
    class PlayerService : IPlayerService, IDisposable
    {
        private Multimedia.Player _player;
        private PlayerState _playerState = PlayerState.Idle;

        public event PlayerStateChangedEventHandler StateChanged;
        public event EventHandler PlaybackCompleted;

        public int Duration => _player == null ? 0 : _player.StreamInfo.GetDuration();

        public int CurrentPosition => _player == null ? 0 : _player.GetPlayPosition();

        public PlayerState State
        {
            get { return _playerState; }
            private set
            {
                _playerState = value;
                StateChanged?.Invoke(this, new PlayerStateChangedEventArgs(_playerState));
            }
        }

        public PlayerService()
        {
            _player = new Multimedia.Player();

            _player.PlaybackCompleted += (s, e) =>
            {
                PlaybackCompleted?.Invoke(this, e);
                State = PlayerState.Stopped;
            };
        }

        public void Pause()
        {
            if (_player.State == Multimedia.PlayerState.Playing)
            {
                _player.Pause();

                State = PlayerState.Paused;
            }
        }

        public async Task PrepareAsync()
        {
            State = PlayerState.Preparing;

            var display = new Multimedia.Display(Forms.Context.MainWindow);
            _player.Display = display;

            await _player.PrepareAsync();

            State = PlayerState.Prepared;
        }

        public void SeekTo(int to)
        {
            _player.SetPlayPositionAsync(to, false);
        }

        public void SetSource(string uri)
        {
            if (_player.State != Multimedia.PlayerState.Idle)
            {
                _player.Unprepare();
            }

            var mediaSource = new Multimedia.MediaUriSource(uri);
            _player.SetSource(mediaSource);
        }

        public void Start()
        {
            if (_player.State == Multimedia.PlayerState.Ready ||
                _player.State == Multimedia.PlayerState.Paused)
            {
                _player.Start();

                State = PlayerState.Playing;
            }
        }

        public void Stop()
        {
            if (_player.State == Multimedia.PlayerState.Playing ||
                _player.State == Multimedia.PlayerState.Paused)
            {
                _player.Stop();

                State = PlayerState.Stopped;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _player.Dispose();
            }
        }
    }
}
