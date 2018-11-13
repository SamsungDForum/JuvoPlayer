using System;
using System.IO;
using JuvoLogger;
using Tizen.TV.NUI.GLApplication;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tizen.Applications;

namespace JuvoPlayer.OpenGL
{
    internal class Program : TVGLApplication
    {
        private SynchronizationContext _uiContext = null; // needs to be initialized in OnCreate!

        private readonly TimeSpan _progressBarFadeout = TimeSpan.FromMilliseconds(5000);
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

        private bool _startedFromDeeplink = false;

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }

        protected override void OnCreate()
        {
            _uiContext = SynchronizationContext.Current;
            OpenGLLoggerManager.Configure(_uiContext);
            DllImports.Create();
            InitMenu();
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            UpdateUI();
            DllImports.Draw(eglDisplay, eglSurface);
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            // Handle the launch request, show the user the task requested through the "AppControlReceivedEventArgs" parameter
            // Smart Hub Preview function requires the below code to identify which deeplink have to be launched
            ReceivedAppControl receivedAppControl = e.ReceivedAppControl;
            receivedAppControl.ExtraData.TryGet("PAYLOAD", out string payload); // Fetch the JSON metadata defined on the smart Hub preview web server

            if (!string.IsNullOrEmpty(payload))
            {
                char[] charSeparator = new char[] { '&' };
                string[] result = payload.Split(charSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (result.Length > 0)
                    PreviewPayloadHandler(result[0]);
            }           

            base.OnAppControlReceived(e);
        }

        private void InitMenu()
        {
            _resourceLoader = new ResourceLoader
            {
                Logger = Logger
            };
            _resourceLoader.LoadResources(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), HandleLoadingFinished);
            _metricsHandler = new MetricsHandler();
            SetMenuFooter();
            SetupOptionsMenu();
            SetDefaultMenuState();
        }

        private static unsafe void SetMenuFooter()
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
            _isMenuShown = false;
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
                GC.Collect();
            }
            else
            {
                Logger?.Info($"Unknown key pressed: {key.KeyPressedName}");
            }

            KeyPressedMenuUpdate();
        }

        private void PreviewPayloadHandler(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            try
            {
                var payload = JsonConvert.DeserializeAnonymousType(message, new { values = "" });
                HandleExternalTileSelection(int.Parse(payload.values));
            }
            catch (Exception e)
            {
                Logger.Error("PreviewPayloadHandler exception " + e.Message);
            }
        }

        private void HandleExternalTileSelection(int tileNo)
        {
            _startedFromDeeplink = true;
            if (tileNo >= 0 && tileNo < _resourceLoader.TilesCount)
            {
                _selectedTile = tileNo;
                DllImports.SelectTile(_selectedTile);
                if (_resourceLoader.IsLoadingFinished)
                    HandleExternalPlaybackStart();
            }
        }

        private void HandleLoadingFinished()
        {
            if (_startedFromDeeplink)
                HandleExternalPlaybackStart();
            else
                ShowMenu(true);
        }

        private void HandleExternalPlaybackStart()
        {
            if (_player != null) // it's possible that playback has already started via other control path (PreviewPayloadHandler vs HandleLoadingFinished calls order)
                return;

            if (_selectedTile >= _resourceLoader.TilesCount)
            {
                ShowMenu(true);
                return;
            }
            ShowMenu(false);
            KeyPressedMenuUpdate(); // Playback UI should be visible
            HandlePlaybackStart();
        }

        public async void DisplayAlert(string title, string body, string button)
        {
            ShowAlert(title, body, button);
            await AwaitDisplayAlert();
        }

        public static unsafe void PushLog(string log)
        {
            fixed (byte* text = ResourceLoader.GetBytes(log))
                DllImports.PushLog(text, log.Length);
        }

        private unsafe void ShowAlert(string title, string body, string button)
        {
            fixed (byte* titleBytes = ResourceLoader.GetBytes(title))
            fixed (byte* bodyBytes = ResourceLoader.GetBytes(body))
            fixed (byte* buttonBytes = ResourceLoader.GetBytes(button))
                DllImports.ShowAlert(titleBytes, title.Length, bodyBytes, body.Length, buttonBytes, button.Length);
            _isAlertShown = true;
        }

        private async Task AwaitDisplayAlert()
        {
            while (_isAlertShown)
                await Task.Delay(100);
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
                Seek(_defaultSeekTime);
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
                Seek(-_defaultSeekTime);
            }
        }

        private void HandleKeyUp()
        {
            if (!_isMenuShown && _options.Visible)
            {
                _options.ControlUp();
            }
            else if (!_isMenuShown && !_options.Visible && _progressBarShown)
            {
                _options.Show();
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
            else if (_progressBarShown && !_options.Visible)
            {
                switch (_player.State)
                {
                    case PlayerState.Playing:
                        _player?.Pause();
                        break;
                    case PlayerState.Paused:
                        _player?.Start();
                        break;
                }
            }
            else if (_options.Visible && _options.ProperSelection())
            {
                _options.ControlSelect(_player);
                _options.Hide();
            }
        }

        private void HandlePlaybackStart()
        {
            if (_player == null)
            {
                _player = new Player();
                _player.StateChanged += (object sender, PlayerStateChangedEventArgs e) =>
                {
                    _uiContext.Post(state =>
                        {
                            Logger?.Info($"Player state changed: {e.State.ToString() ?? "Unknown state"}");
                            if (e.State == PlayerState.Prepared)
                            {
                                if (_player == null)
                                    return;
                                _options.LoadStreamLists(_player);
                                _player.Start();
                            }
                            else if (e.State == PlayerState.Completed)
                            {
                                _playbackCompletedNeedsHandling = true;
                            }
                            else if (e.State == PlayerState.Error)
                            {
                                Logger?.Info($"Playback Error: {(e as PlayerStateChangedStreamError)?.Message ?? "Unknown Error"}");
                                ReturnToMainMenu();
                                DisplayAlert("Playback Error", (e as PlayerStateChangedStreamError)?.Message ?? "Unknown Error", "OK");
                            }
                        },
                        null);
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
        }

        private void ReturnToMainMenu()
        {
            ResetPlaybackControls();
            ShowMenu(true);
            _progressBarShown = false;
            ClosePlayer();
        }

        private void HandleKeyBack()
        {
            if (!_isMenuShown && !_options.Visible)
                ReturnToMainMenu();
            else if (_options.Visible)
                _options.Hide();
            else
                Exit();
        }

        private void ClosePlayer()
        {
            Logger?.Info("Closing player");
            _player?.Stop();
            _player?.Dispose();
            _player = null;
        }

        private void HandleKeyExit()
        {
            ClosePlayer();
            Exit();
        }

        private void HandleKeyPlay()
        {
            if(_player?.State == PlayerState.Playing)
                _player?.Pause();
            else
                _player?.Start();
        }

        private void HandleKeyPause()
        {
            _player?.Pause();
        }

        private void HandleKeyStop()
        {
            ReturnToMainMenu();
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
            if (_player.IsSeekingSupported == false)
                return;

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

        private static unsafe void ResetPlaybackControls()
        {
            DllImports.UpdatePlaybackControls(0, 0, 0, 0, null, 0);
        }

        private void ShowMenu(bool show)
        {
            if (show == _isMenuShown)
            {
                Logger.Info($"ShowMenu({show}) dupe, ignoring.");
                return;
            }

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
            if (_progressBarShown && _player?.State == PlayerState.Playing && (DateTime.Now - _lastKeyPressTime).TotalMilliseconds >= _progressBarFadeout.TotalMilliseconds)
            {
                _progressBarShown = false;
                _options.Hide();
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