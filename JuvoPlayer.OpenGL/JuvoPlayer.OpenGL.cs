using System;
using System.IO;
using JuvoLogger;
using Tizen.TV.NUI.GLApplication;
using System.Linq;
using System.Threading.Tasks;

namespace JuvoPlayer.OpenGL
{
    internal class Program : TVGLApplication
    {
        private readonly TimeSpan _prograssBarFadeout = TimeSpan.FromMilliseconds(5000);
        private readonly TimeSpan _defaultSeekTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _defaultSeekAccumulateTime = TimeSpan.FromMilliseconds(1000);

        private DateTime _lastKeyPressTime;
        private int _selectedTile;
        private bool _isMenuShown;
        private bool _progressBarShown;

        private Player _player;
        private TimeSpan _playerTimeCurrentPosition;
        private TimeSpan _playerTimeDuration;
        private bool _playbackCompletedNeedsHandling;

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private OptionsMenu _options;
        private ResourceLoader _resourceLoader;
        private Metrics _metrics;

        private TimeSpan _accumulatedSeekTime;
        private DateTime _lastSeekTime;
        private bool _seekBufferingInProgress = false;
        private static readonly object _seekLock = new object();

        protected override void OnCreate()
        {
            DllImports.Create();
            InitMenu();
        }

        private void InitMenu()
        {
            _resourceLoader = new ResourceLoader
            {
                Logger = Logger
            };
            _resourceLoader.LoadResources(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)));
            _metrics = new Metrics();
            SetMenuFooter();
            SetupOptionsMenu();
            SetDefaultMenuState();
        }

        private unsafe void SetMenuFooter()
        {
            var footer = "JuvoPlayer Prealpha, OpenGL UI #" + DllImports.OpenGLLibVersion().ToString("x") +
                            ", Samsung R&D Poland 2017-2018";
            fixed (byte* f = ResourceLoader.GetBytes(footer))
                DllImports.SetFooter(f, footer.Length);
        }

        private void SetDefaultMenuState()
        {
            _selectedTile = 0;
            DllImports.SelectTile(_selectedTile);
            _isMenuShown = true;
            DllImports.ShowLoader(1, 0);

            _lastKeyPressTime = DateTime.Now;
            _accumulatedSeekTime = TimeSpan.Zero;
            _lastSeekTime = DateTime.MinValue;

            _playerTimeCurrentPosition = TimeSpan.Zero;
            _playerTimeDuration = TimeSpan.Zero;
            _playbackCompletedNeedsHandling = false;

            _metrics.Hide();
        }

        private void SetupOptionsMenu()
        {
            _options = new OptionsMenu
            {
                Logger = Logger
            };
        }

        protected override void OnKeyEvent(Key key)
        {
            if (key.State != Key.StateType.Down)
                return;

            if(key.KeyPressedName.Contains("Right"))
            {
                HandleKeyRight();
            }
            else if(key.KeyPressedName.Contains("Left"))
            {
                HandleKeyLeft();
            }
            else if(key.KeyPressedName.Contains("Up"))
            {
                HandleKeyUp();
            }
            else if(key.KeyPressedName.Contains("Down"))
            {
                HandleKeyDown();
            }
            else if(key.KeyPressedName.Contains("Return"))
            {
                HandleKeyReturn();
            }
            else if(key.KeyPressedName.Contains("Back"))
            {
                HandleKeyBack();
            }
            else if(key.KeyPressedName.Contains("Exit"))
            {
                HandleKeyExit();
            }
            else if(key.KeyPressedName.Contains("Play") || key.KeyPressedName.Contains("3XSpeed"))
            {
                HandleKeyPlay();
            }
            else if(key.KeyPressedName.Contains("Pause"))
            {
                HandleKeyPause();
            }
            else if(key.KeyPressedName.Contains("Stop") || key.KeyPressedName.Contains("3D"))
            {
                HandleKeyStop();
            }
            else if(key.KeyPressedName.Contains("Rewind"))
            {
                HandleKeyRewind();
            }
            else if (key.KeyPressedName.Contains("Next"))
            {
                HandleKeySeekForward();
            }
            else if(key.KeyPressedName.Contains("Red"))
            {
                if(_metrics.IsShown())
                    _metrics.Hide();
                else
                    _metrics.Show();
            }
            else if(key.KeyPressedName.Contains("Green"))
            {
                ShowMenu(!_isMenuShown);
            }
            else if(key.KeyPressedName.Contains("Yellow"))
            {
                DllImports.SwitchTextRenderingMode();
            }
            else if(key.KeyPressedName.Contains("Blue"))
            {
            }
            else
            {
                Logger?.Info("Unknown key pressed: " + key.KeyPressedName);
            }

            KeyPressedMenuUpdate();
        }

        private void KeyPressedMenuUpdate()
        {
            _lastKeyPressTime = DateTime.Now;
            _progressBarShown = !_isMenuShown;
            if (!_progressBarShown && _options.Visible)
                _options.Hide();
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            _resourceLoader.LoadQueuedResources();
            UpdateUI();
            DllImports.Draw(eglDisplay, eglSurface);
        }

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }

        private void HandleKeyRight()
        {
            if (_isMenuShown)
            {
                if (_selectedTile < _resourceLoader.TilesCount - 1)
                    _selectedTile = (_selectedTile + 1) % _resourceLoader.TilesCount;
                DllImports.SelectTile(_selectedTile);
            }
            else if (_options.Visible)
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
            if (_isMenuShown)
            {
                if (_selectedTile > 0)
                    _selectedTile = (_selectedTile - 1 + _resourceLoader.TilesCount) % _resourceLoader.TilesCount;
                DllImports.SelectTile(_selectedTile);
            }
            else if (_options.Visible)
            {
                _options.ControlLeft();
            }
        }

        private void HandleKeyUp()
        {
            if (!_isMenuShown && _options.Visible)
            {
                _options.ControlUp();
            }
        }

        private void HandleKeyDown()
        {
            if (!_isMenuShown && _options.Visible)
                _options.ControlDown();
        }

        private void HandleKeyReturn()
        {
            if (_isMenuShown)
            {
                if (_selectedTile >= _resourceLoader.TilesCount)
                    return;
                ShowMenu(false);
                HandlePlaybackStart();
            }
            else if (_options.Visible)
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
            {
                _player = new Player();
                _player.StateChanged += (object sender, PlayerState playerState) =>
                {
                    Logger?.Info("Player state changed: " + _player.State);
                    if (_player.State == PlayerState.Prepared)
                        _player?.Start();
                    else if (_player.State == PlayerState.Completed)
                        _playbackCompletedNeedsHandling = true;
                };
            }

            Logger?.Info("Playing " + _resourceLoader.ContentList[_selectedTile].Title + " (" + _resourceLoader.ContentList[_selectedTile].Url + ")");
            _player.SetSource(_resourceLoader.ContentList[_selectedTile]);
            _options.LoadStreamLists(_player);
            _seekBufferingInProgress = false;
        }

        private void HandleKeyBack()
        {
            if (!_isMenuShown && !_options.Visible)
            {
                ResetPlaybackControls();
                ShowMenu(true);
                ClosePlayer();
            }
            else if (_options.Visible)
            {
                _options.Hide();
            }
        }

        private void ClosePlayer()
        {
            if (_player == null)
                return;
            _player.Dispose();
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

            lock (_seekLock)
                _lastSeekTime = DateTime.Now;

            if (_seekBufferingInProgress == false)
            {
                lock (_seekLock)
                {
                    _accumulatedSeekTime = seekTime;
                    _seekBufferingInProgress = true;
                }

                Task.Run(() => SeekBufferingTask());
            }
            else
            {
                lock (_seekLock)
                    _accumulatedSeekTime += seekTime;
            }

            _playerTimeCurrentPosition += seekTime;
            UpdatePlaybackControls();
        }

        private async void SeekBufferingTask()
        {
            DateTime lastSeekTime;
            lock (_seekLock)
                lastSeekTime = _lastSeekTime;
            while (lastSeekTime + _defaultSeekAccumulateTime > DateTime.Now)
            {
                TimeSpan delay = (lastSeekTime + _defaultSeekAccumulateTime) - DateTime.Now;
                await Task.Delay(delay);
                lock (_seekLock)
                    lastSeekTime = _lastSeekTime;
            }

            TimeSpan accumulatedSeekTime;
            lock (_seekLock)
            {
                accumulatedSeekTime = _accumulatedSeekTime;
                _seekBufferingInProgress = false;
            }

            if (accumulatedSeekTime > TimeSpan.Zero)
                Forward(accumulatedSeekTime);
            else
                Rewind(-accumulatedSeekTime);
        }

        private static bool IsStateSeekable(PlayerState state)
        {
            var seekableStates = new[] { PlayerState.Playing, PlayerState.Paused };
            return seekableStates.Contains(state);
        }

        private void Forward(TimeSpan seekTime)
        {
            if (_player == null || !_player.IsSeekingSupported || !IsStateSeekable(_player.State))
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

        private unsafe void ResetPlaybackControls()
        {
            DllImports.UpdatePlaybackControls(0, 0, 0, 0, null, 0);
        }

        private void ShowMenu(bool show)
        {
            _isMenuShown = show;
            DllImports.ShowMenu(_isMenuShown ? 1 : 0);
        }

        private unsafe void UpdateSubtitles()
        {
            if (_player?.CurrentCueText != null && _options.SubtitlesOn)
            {
                fixed (byte* cueText = ResourceLoader.GetBytes(_player.CurrentCueText))
                    DllImports.ShowSubtitle(0, cueText, _player.CurrentCueText.Length); // 0ms duration - special value just for next frame
            }
        }

        private void UpdatePlaybackCompleted() // doesn't work from side thread
        {
            if (!_playbackCompletedNeedsHandling)
                return;

            _playbackCompletedNeedsHandling = false;
            Logger?.Info("Playback completed. Returning to menu.");
            if (_isMenuShown)
                return;
            _progressBarShown = false;
            _options.Hide();
            ResetPlaybackControls();
            ShowMenu(true);
            ClosePlayer();
            _seekBufferingInProgress = false;
        }

        private void UpdateMetrics()
        {
            _metrics.Update();
        }

        private unsafe void UpdatePlaybackControls()
        {
            if (_seekBufferingInProgress == false)
                _playerTimeCurrentPosition = _player?.CurrentPosition ?? TimeSpan.Zero;
            _playerTimeDuration = _player?.Duration ?? TimeSpan.Zero;
            if (_progressBarShown && _player?.State == PlayerState.Playing && (DateTime.Now - _lastKeyPressTime).TotalMilliseconds >= _prograssBarFadeout.TotalMilliseconds)
            {
                _progressBarShown = false;
                _options.Hide();
                Logger?.Info((DateTime.Now - _lastKeyPressTime).TotalMilliseconds + "ms of inactivity, hiding progress bar.");
            }

            fixed (byte* name = ResourceLoader.GetBytes(_resourceLoader.ContentList[_selectedTile].Title))
                DllImports.UpdatePlaybackControls(_progressBarShown ? 1 : 0, (int)(_player?.State ?? PlayerState.Idle), (int)_playerTimeCurrentPosition.TotalMilliseconds,
                    (int)_playerTimeDuration.TotalMilliseconds, name, _resourceLoader.ContentList[_selectedTile].Title.Length);
        }

        private void UpdateUI()
        {
            UpdateSubtitles();
            UpdatePlaybackCompleted();
            UpdatePlaybackControls();
            UpdateMetrics();
        }
    }
}