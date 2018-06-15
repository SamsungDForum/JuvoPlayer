using System;
using System.IO;
using JuvoLogger;
using Tizen.TV.NUI.GLApplication;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tizen;

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

        private const string Tag = "JuvoPlayer";
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private MenuAction _selectedAction = MenuAction.None;

        private OptionsMenu _options;
        private ResourceLoader _resourceLoader;
        private MetricsHandler _metricsHandler;

        private TimeSpan _accumulatedSeekTime;
        private Task _seekDelay;
        private CancellationTokenSource _seekCancellationTokenSource;
        private bool _seekBufferingInProgress = false;
        private bool _seekInProgress = false;

        private bool _isAlertShown = false;

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }

        protected override void OnCreate()
        {
            DllImports.Create();
            InitMenu();
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            UpdateUI();
            DllImports.Draw(eglDisplay, eglSurface);
        }

        private void InitMenu()
        {
            _resourceLoader = new ResourceLoader
            {
                Logger = Logger
            };
            _resourceLoader.LoadResources(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)));
            _metricsHandler = new MetricsHandler();
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

            _playerTimeCurrentPosition = TimeSpan.Zero;
            _playerTimeDuration = TimeSpan.Zero;
            _playbackCompletedNeedsHandling = false;

            _metricsHandler.Hide();
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

            if (_isAlertShown && !key.KeyPressedName.Contains("Return") && !key.KeyPressedName.Contains("Exit"))
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
                _metricsHandler.SwitchVisibility();
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
                GC.Collect();
            }
            else if (key.KeyPressedName.Contains("1"))
            {
                TestLog();
            }
            else if (key.KeyPressedName.Contains("2"))
            {
                ShowAlert("Alert", "All your base are belong to us.");
            }
            else
            {
                Logger?.Info($"Unknown key pressed: {key.KeyPressedName}");
            }

            KeyPressedMenuUpdate();
        }

        private unsafe void ShowAlert(string title, string text)
        {
            fixed (byte* textBytes = ResourceLoader.GetBytes(text))
                fixed (byte* titleBytes = ResourceLoader.GetBytes(title))
                    DllImports.ShowAlert(titleBytes, title.Length, textBytes, text.Length);
            _isAlertShown = true;
        }

        private int testLog = 0;

        private unsafe void TestLog()
        {
            Random rnd = new Random();
            string[] logs =
            {
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
                "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
                "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.",
                "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum."
            };
            string log = (++testLog).ToString() + ". " + logs[rnd.Next() % logs.Length];
            fixed (byte* text = ResourceLoader.GetBytes(log))
                DllImports.PushLog(text, log.Length);
        }

        private void KeyPressedMenuUpdate()
        {
            _lastKeyPressTime = DateTime.Now;
            _progressBarShown = !_isMenuShown;
            if (!_progressBarShown && _options.Visible)
                _options.Hide();
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
            else if(_progressBarShown)
            {
                switch (_selectedAction)
                {
                    case MenuAction.PlaybackControl:
                        break;
                    case MenuAction.OptionsMenu:
                        SelectMenuAction(MenuAction.PlaybackControl);
                        break;
                    case MenuAction.None:
                        break;
                }
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
            else if (_progressBarShown)
            {
                switch (_selectedAction)
                {
                    case MenuAction.PlaybackControl:
                        SelectMenuAction(MenuAction.OptionsMenu);
                        break;
                    case MenuAction.OptionsMenu:
                        break;
                    case MenuAction.None:
                        break;
                }
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
            {
                _options.ControlDown();
            }
        }

        private void HandleKeyReturn()
        {
            if (_isAlertShown)
            {
                DllImports.HideAlert();
                _isAlertShown = false;
            }
            else if (_isMenuShown)
            {
                if (_selectedTile >= _resourceLoader.TilesCount)
                    return;
                ShowMenu(false);
                HandlePlaybackStart();
            }
            else
            {
                switch (_selectedAction)
                {
                    case MenuAction.PlaybackControl:
                        switch (_player.State)
                        {
                            case PlayerState.Playing:
                                _player?.Pause();
                                break;
                            case PlayerState.Paused:
                                _player?.Start();
                                break;
                        }
                        break;
                    case MenuAction.OptionsMenu:
                        if (_options.Visible && _options.ProperSelection())
                        {
                            _options.ControlSelect(_player);
                            _options.Hide();
                        }
                        else if (!_options.Visible)
                        {
                            _options.Show();
                        }
                        break;
                    case MenuAction.None:
                        break;
                }
            }
        }

        private void HandlePlaybackStart()
        {
            if (_player == null)
            {
                _player = new Player();
                _player.StateChanged += (object sender, PlayerState playerState) =>
                {
                    Logger?.Info($"Player state changed: {_player.State}");
                    if (_player.State == PlayerState.Prepared)
                    {
                        _options.LoadStreamLists(_player);
                        SelectMenuAction(MenuAction.PlaybackControl);
                        _player?.Start();
                    }
                    else if (_player.State == PlayerState.Completed)
                        _playbackCompletedNeedsHandling = true;
                };
                _player.SeekCompleted += () =>
                {
                    Logger?.Info("Seek completed.");
                    _seekInProgress = false;
                };
            }

            Logger?.Info($"Playing {_resourceLoader.ContentList[_selectedTile].Title} ({_resourceLoader.ContentList[_selectedTile].Url})");
            _player.SetSource(_resourceLoader.ContentList[_selectedTile]);
            _options.ClearOptionsMenu();
            _seekInProgress = false;
            SelectMenuAction(MenuAction.None);
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
            ClosePlayer();
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
            if (!_isMenuShown && !_options.Visible)
            {
                ResetPlaybackControls();
                ShowMenu(true);
            }
            _player?.Stop();
            ClosePlayer();
        }

        private void HandleKeyRewind()
        {
            Seek(-_defaultSeekTime);
        }

        private void HandleKeySeekForward()
        {
            Seek(_defaultSeekTime);
        }

        private async void Seek(TimeSpan seekTime)
        {
            _seekCancellationTokenSource?.Cancel();

            if (_seekBufferingInProgress == false)
            {
                _accumulatedSeekTime = seekTime;
                _seekBufferingInProgress = true;
            }
            else
            {
                _accumulatedSeekTime += seekTime;
            }
            _playerTimeCurrentPosition += seekTime;
            UpdatePlaybackControls();

            _seekCancellationTokenSource = new CancellationTokenSource();
            _seekDelay = Task.Delay(_defaultSeekAccumulateTime, _seekCancellationTokenSource.Token);
            try
            {
                await _seekDelay;
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (_accumulatedSeekTime > TimeSpan.Zero)
                Forward(_accumulatedSeekTime);
            else
                Rewind(-_accumulatedSeekTime);
            _seekBufferingInProgress = false;

            _seekDelay = null;
            _seekCancellationTokenSource = null;
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

            _seekInProgress = true;
            if (_player.Duration - _player.CurrentPosition < seekTime)
                _player.SeekTo(_player.Duration);
            else
                _player.SeekTo(_player.CurrentPosition + seekTime);
        }

        private void Rewind(TimeSpan seekTime)
        {
            if (_player == null || (!_player.IsSeekingSupported || _player.State < PlayerState.Playing))
                return;

            _seekInProgress = true;
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
            Logger?.Info($"Playback completed. Returning to menu.");
            if (_isMenuShown)
                return;
            _progressBarShown = false;
            _options.Hide();
            SelectMenuAction(MenuAction.None);
            ResetPlaybackControls();
            ShowMenu(true);
            ClosePlayer();
            _seekBufferingInProgress = false;
        }

        private void UpdateMetrics()
        {
            _metricsHandler.Update();
        }

        private unsafe void UpdatePlaybackControls()
        {
            if (_seekBufferingInProgress == false && _seekInProgress == false)
                _playerTimeCurrentPosition = _player?.CurrentPosition ?? TimeSpan.Zero;
            _playerTimeDuration = _player?.Duration ?? TimeSpan.Zero;
            if (_progressBarShown && _player?.State == PlayerState.Playing && (DateTime.Now - _lastKeyPressTime).TotalMilliseconds >= _prograssBarFadeout.TotalMilliseconds)
            {
                _progressBarShown = false;
                _options.Hide();
                SelectMenuAction(MenuAction.PlaybackControl);
                Logger?.Info($"{(DateTime.Now - _lastKeyPressTime).TotalMilliseconds} ms of inactivity, hiding progress bar.");
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

        private void SelectMenuAction(MenuAction menuAction)
        {
            _selectedAction = menuAction;
            DllImports.SelectAction((int)_selectedAction);
        }
    }
}