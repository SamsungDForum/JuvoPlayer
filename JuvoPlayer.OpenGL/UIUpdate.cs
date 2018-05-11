using System;
using JuvoPlayer.OpenGL.Services;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program
    {
        private void UpdateSubtitles()
        {
            if (_player?.CurrentCueText != null && _options.SubtitlesOn())
            {
                fixed (byte* cueText = ResourceLoader.GetBytes(_player.CurrentCueText))
                    DllImports.ShowSubtitle(0, cueText, _player.CurrentCueText.Length); // 0ms duration - special value just for next frame
            }
        }

        private void UpdatePlaybackCompleted() // doesn't work from side thread
        {
            if (!_handlePlaybackCompleted)
                return;

            _handlePlaybackCompleted = false;
            Logger?.Info("Playback completed. Returning to menu.");
            if (_menuShown)
                return;
            _progressBarShown = false;
            _options.Hide();
            _menuShown = true;
            DllImports.UpdatePlaybackControls(0, 0, 0, 0, null, 0);
            DllImports.ShowMenu(_menuShown ? 1 : 0);

            if (_player != null)
            {
                _player.Dispose(); // TODO(g.skowinski): Check wheter it's the best way
                _player = null;
            }
        }

        private void UpdatePlaybackControls()
        {
            if (_player == null)
                _playerState = (int)PlayerState.Idle;
            if(_seekTask == null)
                _playerTimeCurrentPosition = (int)(_player?.CurrentPosition.TotalMilliseconds ?? 0);
            _playerTimeDuration = (int)(_player?.Duration.TotalMilliseconds ?? 0);
            if (_progressBarShown && _playerState == (int)PlayerState.Playing &&
                (DateTime.Now - _lastAction).TotalMilliseconds >= _prograssBarFadeout.TotalMilliseconds)
            {
                _progressBarShown = false;
                _options.Hide();
                Logger?.Info((DateTime.Now - _lastAction).TotalMilliseconds + "ms of inactivity, hiding progress bar.");
            }

            fixed (byte* name = ResourceLoader.GetBytes(_resourceLoader.ContentList[_selectedTile].Title))
                DllImports.UpdatePlaybackControls(_progressBarShown ? 1 : 0, _playerState, _playerTimeCurrentPosition,
                    _playerTimeDuration, name, _resourceLoader.ContentList[_selectedTile].Title.Length);
        }

        private void UpdateUI()
        {
            UpdateSubtitles();
            UpdatePlaybackCompleted();
            UpdatePlaybackControls();
        }
    }
}