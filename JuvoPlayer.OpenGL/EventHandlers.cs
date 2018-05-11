using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.OpenGL.Services;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program
    {
        private TimeSpan _accumulatedSeekTime;
        private DateTime _lastSeekTime;
        private Task _seekTask;

        private void HandleKeyRight()
        {
            if (_menuShown)
            {
                if (_selectedTile < _resourceLoader.TilesNumber - 1)
                    _selectedTile = (_selectedTile + 1) % _resourceLoader.TilesNumber;
                DllImports.SelectTile(_selectedTile);
            }
            else if (_options.IsShown())
            {
                _options.ControlRight();
            }
            else if (_progressBarShown)
            {
                _options.Show();
            }
        }

        private void HandleKeyLeft()
        {
            if (_menuShown)
            {
                if (_selectedTile > 0)
                    _selectedTile = (_selectedTile - 1 + _resourceLoader.TilesNumber) % _resourceLoader.TilesNumber;
                DllImports.SelectTile(_selectedTile);
            }
            else if (_options.IsShown())
            {
                _options.ControlLeft();
            }
        }

        private void HandleKeyUp()
        {
            if (!_menuShown && _options.IsShown())
            {
                _options.ControlUp();
            }
        }

        private void HandleKeyDown()
        {
            if (!_menuShown && _options.IsShown())
                _options.ControlDown();
        }

        private void HandleKeyReturn()
        {
            if (_menuShown)
            {
                if (_selectedTile >= _resourceLoader.ContentList.Count)
                    return;
                if (_menuShown)
                {
                    _menuShown = false;
                    DllImports.ShowMenu(_menuShown ? 1 : 0);
                }
                HandlePlaybackStart();
            }
            else if (_options.IsShown())
            {
                _options.ControlSelect(_player);
            }
            else if (_progressBarShown)
            {
                _options.Show();
            }
        }

        private void HandlePlaybackStart()
        {
            if (_player == null)
                _player = new PlayerService();
            _player.StateChanged += (object sender, PlayerStateChangedEventArgs e) =>
            {
                Logger?.Info("Player state changed: " + _player.State);
                _playerState = (int)_player.State;
                if (_player.State == PlayerState.Prepared)
                    _player?.Start();
                else if (_player.State == PlayerState.Completed)
                    _handlePlaybackCompleted = true;
            };
            Logger?.Info("Playing " + _resourceLoader.ContentList[_selectedTile].Title + " (" + _resourceLoader.ContentList[_selectedTile].Source + ")");
            _player.SetSource(_resourceLoader.ContentList[_selectedTile].Clip);
            _options.GetStreams(_player);
        }

        private void HandleKeyBack()
        {
            if (!_menuShown && !_options.IsShown())
            {
                DllImports.UpdatePlaybackControls(0, 0, 0, 0, null, 0);
                _menuShown = true;
                DllImports.ShowMenu(_menuShown ? 1 : 0);
                ClosePlayer();
            }
            else if (_options.IsShown())
            {
                _options.Hide();
            }
        }

        private void ClosePlayer()
        {
            if (_player == null)
                return;
            _player.Dispose(); // TODO(g.skowinski): Check wheter it's the best way
            _player = null;
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

        private void Seek(TimeSpan seekTime)
        {
            if (_player == null)
                return;

            _lastSeekTime = DateTime.Now;

            if (_seekTask == null)
            {
                Logger.Info("Seek buffering: Planning to seek " + _accumulatedSeekTime.ToString() + " in " + ((_lastSeekTime + _defaultSeekAccumulateTime) - DateTime.Now).Milliseconds + "ms (new seek task)");
                _accumulatedSeekTime = seekTime;
                _seekTask = Task.Delay(_defaultSeekAccumulateTime).ContinueWith(_ =>
                {
                    while (_lastSeekTime + _defaultSeekAccumulateTime > DateTime.Now)
                    {
                        TimeSpan delay = (_lastSeekTime + _defaultSeekAccumulateTime) - DateTime.Now;
                        Logger.Info("Seek buffering: waiting for another " + delay.Milliseconds + "ms to seek");
                        Thread.Sleep(delay);
                    }

                    Logger.Info("Seek buffering: Seeking " + _accumulatedSeekTime.ToString());
                    if (_accumulatedSeekTime > TimeSpan.Zero)
                        Forward(_accumulatedSeekTime);
                    else
                        Rewind(-_accumulatedSeekTime);
                    _seekTask = null;
                });
            }
            else
            {
                _accumulatedSeekTime += seekTime;
                Logger.Info("Seek buffering: Planning to seek " + _accumulatedSeekTime.ToString() + " in " + ((_lastSeekTime + _defaultSeekAccumulateTime) - DateTime.Now).Milliseconds + "ms");
            }
            
            _playerTimeCurrentPosition += (int)seekTime.TotalMilliseconds; // seekTime.Milliseconds which would be more direct to use returns unfortunately 0...
            UpdatePlaybackControls(); // TODO(g.skowinski): To fix: after seek command is sent, progress bar returns to current time and then after seek is executed, it jumps to correct time.
        }

        private void Forward(TimeSpan seekTime)
        {
            if (_player == null || (!_player.IsSeekingSupported || _player.State < PlayerState.Playing))
                return;

            if (_player.Duration - _player.CurrentPosition < seekTime)
                _player.SeekTo(_player.Duration);
            else
                _player.SeekTo(_player.CurrentPosition + seekTime);
        }

        private void Rewind(TimeSpan seekTime)
        {
            if (_player == null || (!_player.IsSeekingSupported || _player.State < PlayerState.Playing))
                return;

            if (_player.CurrentPosition < seekTime)
                _player.SeekTo(TimeSpan.Zero);
            else
                _player.SeekTo(_player.CurrentPosition - seekTime);
        }
    }
}