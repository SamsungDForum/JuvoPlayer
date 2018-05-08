using System;
using System.Threading.Tasks;
using JuvoPlayer.OpenGL.Services;
using Tizen;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private void HandleKeyRight()
        {
            if (_menuShown) {
                if (_selectedTile < _tilesNumber - 1)
                    _selectedTile = (_selectedTile + 1) % _tilesNumber;
                SelectTile(_selectedTile);
            }
            else if (_options.IsShown()) {
                _options.ControlRight();
            }
            else if (_progressBarShown) {
                _options.Show();
            }
        }

        private void HandleKeyLeft()
        {
            if (_menuShown) {
                if (_selectedTile > 0)
                    _selectedTile = (_selectedTile - 1 + _tilesNumber) % _tilesNumber;
                SelectTile(_selectedTile);
            }
            else if (_options.IsShown()) {
                _options.ControlLeft();
            }
        }

        private void HandleKeyUp()
        {
            if (!_menuShown && _options.IsShown()) {
                _options.ControlUp();
            }
        }

        private void HandleKeyDown()
        {
            if (!_menuShown && _options.IsShown()) {
                _options.ControlDown();
            }
        }

        private void HandleKeyReturn()
        {
            if (_menuShown) {
                if (_selectedTile >= ContentList.Count)
                    return;
                if (_player == null)
                    _player = new PlayerService();
                _player.PlaybackCompleted += () => { _handlePlaybackCompleted = true; };
                _player.StateChanged += (object sender, PlayerStateChangedEventArgs e) =>
                {
                    Log.Info("JuvoPlayer", "Player state changed: " + _player.State);
                    _playerState = (int)_player.State;
                    if (_player.State == PlayerState.Prepared)
                        _player?.Start();
                };
                Log.Info("JuvoPlayer",
                    "Playing " + ContentList[_selectedTile].Title + " (" + ContentList[_selectedTile].Source +
                    ")");
                _player.SetSource(ContentList[_selectedTile].Clip);
                _options.GetStreams(_player);
                if (!_menuShown)
                    return;
                _menuShown = false;
                ShowMenu(_menuShown ? 1 : 0);
            }
            else if (_options.IsShown()) {
                _options.ControlSelect(_player);
            }
            else if (_progressBarShown) {
                _options.Show();
            }
        }

        private void HandleKeyBack()
        {
            if (!_menuShown && !_options.IsShown()) {
                UpdatePlaybackControls(0, 0, 0, 0, null, 0);
                _menuShown = true;
                ShowMenu(_menuShown ? 1 : 0);
                if (_player != null) {
                    _player.Dispose(); // TODO: Check wheter it's the best way
                    _player = null;
                }
            }
            else if (_options.IsShown()) {
                _options.Hide();
            }
        }

        private void HandleKeyExit()
        {
            Exit();
        }

        private void HandleKeyPlay()
        {
            _player?.Start();
        }

        private void HandleKeyPause()
        {
            _player?.Pause();
        }

        private void HandleKeyStop()
        {
            _player?.Stop();
        }

        private void HandleKeyRewind()
        {
            Seek(-_defaultSeekTime);
        }

        private void HandleKeySeekForward()
        {
            Seek(_defaultSeekTime);
        }

        private void Seek(TimeSpan seekTime) {
            if (_player != null) {
                Log.Info("JuvoPlayer", "Accumulating seek time " + _accumulatedSeekTime + " + " + seekTime);
                if (_seekTask == null) {
                    _accumulatedSeekTime = seekTime;
                    _seekTask = Task.Delay(_defaultSeekAccumulateTime).ContinueWith(_ => {
                        Log.Info("JuvoPlayer", "Seeking " + _accumulatedSeekTime.ToString());
                        _seekTask = null;
                        if (_accumulatedSeekTime > TimeSpan.Zero)
                            Forward(_accumulatedSeekTime);
                        else
                            Rewind(-_accumulatedSeekTime);
                    });
                }
                else {
                    _accumulatedSeekTime += seekTime;
                }
            }
        }

        private void Forward(TimeSpan seekTime) {
            if (!_player.IsSeekingSupported || _player.State < PlayerState.Playing)
                return;

            if (_player.Duration - _player.CurrentPosition < seekTime)
                _player.SeekTo(_player.Duration);
            else
                _player.SeekTo(_player.CurrentPosition + seekTime);
        }

        private void Rewind(TimeSpan seekTime) {
            if (!_player.IsSeekingSupported || _player.State < PlayerState.Playing)
                return;

            if (_player.CurrentPosition < seekTime)
                _player.SeekTo(TimeSpan.Zero);
            else
                _player.SeekTo(_player.CurrentPosition - seekTime);
        }
    }
}