/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using JuvoPlayer.Common;
using JuvoLogger;
using Tizen.TV.NUI.GLApplication;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElmSharp;
using Tizen.Applications;
using Tizen.System;

namespace JuvoPlayer.OpenGL
{
    internal class Program : TVGLApplication, ISeekLogicClient
    {
        private SynchronizationContext _uiContext = null; // needs to be initialized in OnCreate!

        private readonly TimeSpan _progressBarFadeout = TimeSpan.FromMilliseconds(5000);

        private SeekLogic _seekLogic = null; // needs to be initialized in OnCreate!

        private DateTime _lastKeyPressTime;
        private int _selectedTile = -1;
        private bool _isMenuShown;
        private bool _progressBarShown;

        public IPlayerService Player { get; private set; }

        private bool _playbackCompletedNeedsHandling;

        private const string Tag = "JuvoPlayer";
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private OptionsMenu _options;
        private ResourceLoader _resourceLoader;
        private MetricsHandler _metricsHandler;

        private bool _isAlertShown;
        private string _deepLinkUrl = "";

        private readonly SystemMemoryUsage _systemMemoryUsage = new SystemMemoryUsage();
        private int _systemMemoryUsageGraphId;
        private float _systemMemoryBottom;
        private float _systemMemoryTop;

        private bool _bufferingInProgress = false;
        private int _bufferingProgress = 0;

        private readonly SystemCpuUsage _systemCpuUsage = new SystemCpuUsage();

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }

        protected override void OnCreate()
        {
            _uiContext = SynchronizationContext.Current;
            OpenGlLoggerManager.Configure(_uiContext);
            _seekLogic = new SeekLogic(this);
            DllImports.Create();
            InitializeKeyHandlersMap();
            InitMenu();
        }

        protected override void OnTerminate()
        {
            DllImports.Terminate();
        }

        protected override bool OnUpdate()
        {
            UpdateUi();
            NativeActions.GetInstance().Execute();
            DllImports.Draw();
            return true;
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e) // Launch request handling via Smart Hub Preview (deep links) functionality
        {
            var payloadParser = new PayloadParser(e.ReceivedAppControl);
            if (!payloadParser.TryGetUrl(out var url))
                return;
            _deepLinkUrl = url;
            if (_resourceLoader.IsLoadingFinished)
                HandleExternalTileSelection();
            base.OnAppControlReceived(e);
        }

        private bool _appPaused;
        private Window _playerWindow;

        protected override void OnPause()
        {
            base.OnPause();
            Player?.Suspend();

            _appPaused = true;
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!_appPaused)
                return;

            _appPaused = false;

            if (Player != null)
            {
                ShowMenu(false);
                KeyPressedMenuUpdate(); // Playback UI should be visible when starting playback after app execution is resumed
                Player.Resume();
                return;
            }

            ShowMenu(true);
        }

        private void InitMenu()
        {
            SetLoaderLogo(Path.Combine(Current.DirectoryInfo.SharedResource, "JuvoPlayerOpenGLNativeTizenTV.png"));

            _resourceLoader = ResourceLoader.GetInstance();
            _resourceLoader.LoadResources(
                Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)),
                HandleLoadingError, HandleLoadingFinished);
            _metricsHandler = new MetricsHandler();
            SetMetrics();
            SetMenuFooter();
            SetupOptionsMenu();
            SetDefaultMenuState();
        }

        private async void SetLoaderLogo(string path)
        {
            var imageData = await Resource.GetImage(path);
            NativeActions.GetInstance().Enqueue(() =>
            {
                unsafe
                {
                    fixed (byte* pixels = imageData.Pixels)
                        DllImports.SetLoaderLogo(new DllImports.ImageData
                        {
                            id = 0,
                            pixels = pixels,
                            width = imageData.Width,
                            height = imageData.Height,
                            format = (int)imageData.Format
                        });
                }
            });
        }

        private void SetMetrics()
        {
            _systemMemoryBottom = (float)_systemMemoryUsage.Used / 1024;
            _systemMemoryTop = (float)_systemMemoryUsage.Total / 1024;
            _systemMemoryUsageGraphId = _metricsHandler.AddMetric("MEM", (float)_systemMemoryUsage.Used / 1024,
                (float)_systemMemoryUsage.Total / 1024, 100,
                () =>
                {
                    try
                    {
                        _systemMemoryUsage.Update();
                    }
                    catch
                    {
                        /* ignore */
                    }

                    if (_systemMemoryBottom > (float)_systemMemoryUsage.Used / 1024)
                    {
                        _systemMemoryBottom = (float)_systemMemoryUsage.Used / 1024;
                        _metricsHandler.UpdateGraphRange(_systemMemoryUsageGraphId, _systemMemoryBottom,
                            _systemMemoryTop);
                    }

                    return (float)_systemMemoryUsage.Used / 1024;
                });

            _metricsHandler.AddMetric("CPU", 0, 100, 100,
                () =>
                {
                    try
                    {
                        _systemCpuUsage.Update();
                    }
                    catch
                    {
                        /* ignore */
                    } // underlying code is broken - it takes only one sample from /proc/stat, so it's giving average load from system boot till now (like "top -n1" => us + sy + ni)

                    return (float)(_systemCpuUsage.User + _systemCpuUsage.Nice + _systemCpuUsage.System);
                });
        }

        private static unsafe void SetMenuFooter()
        {
            string footer =
                $"JuvoPlayer v{typeof(Program).Assembly.GetName().Version.Major}.{typeof(Program).Assembly.GetName().Version.Minor}.{typeof(Program).Assembly.GetName().Version.Build}, OpenGL Native #{DllImports.OpenGLLibVersion():x}, Samsung R&D Poland 2017-{DateTime.Now.Year}";
            fixed (byte* f = ResourceLoader.GetBytes(footer))
                DllImports.SetFooter(f, footer.Length);
        }

        private void SetDefaultMenuState()
        {
            _isMenuShown = false;

            DllImports.ShowLoader(1, 0);

            _lastKeyPressTime = DateTime.Now;
            _seekLogic.Reset();

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

        private delegate void KeyHandlerDelegate();

        private Dictionary<string, KeyHandlerDelegate> _keyHandlersMap;

        private void InitializeKeyHandlersMap()
        {
            _keyHandlersMap = new Dictionary<string, KeyHandlerDelegate>()
            {
                { "Right", HandleKeyRight },
                { "Left", HandleKeyLeft },
                { "Up", HandleKeyUp },
                { "Down", HandleKeyDown },
                { "Return", HandleKeyReturn },
                { "Back", HandleKeyBack },
                { "Exit", HandleKeyExit },
                { "Play", HandleKeyPlay },
                { "3XSpeed", HandleKeyPlay },
                { "Pause", HandleKeyPause },
                { "Stop", HandleKeyStop },
                { "3D", HandleKeyStop },
                { "Rewind", HandleKeyRewind },
                { "Next", HandleKeySeekForward },
                { "Red", HandleKeyRed },
                { "Green", HandleKeyGreen }
            };
        }

        protected override void OnKeyEvent(Key key)
        {
            if (key.State != Key.StateType.Down)
                return;

            if (_isAlertShown && !key.KeyPressedName.Contains("Return") && !key.KeyPressedName.Contains("Exit"))
                return;

            foreach ((string keyName, KeyHandlerDelegate keyHandler) in _keyHandlersMap)
            {
                if (!key.KeyPressedName.Contains(keyName))
                    continue;
                keyHandler?.Invoke();
                break;
            }

            KeyPressedMenuUpdate();
        }

        private void HandleExternalTileSelection()
        {
            var tileNo = _resourceLoader.ContentList.FindIndex(content =>
                string.Equals(content.Url, _deepLinkUrl, StringComparison.OrdinalIgnoreCase));
            _deepLinkUrl = "";
            if (tileNo == -1 || tileNo == _selectedTile)
                return;
            if (Player != null)
                ClosePlayer();
            _selectedTile = tileNo;
            DllImports.SelectTile(_selectedTile, 0);
            HandleExternalPlaybackStart();
        }

        private void HandleLoadingFinished()
        {
            _playerWindow = new Window("JuvoPlayer")
            {
                Geometry = new Rect(0, 0, 1920, 1080)
            };
            _playerWindow.Show();
            _playerWindow.Lower();

            if (!_deepLinkUrl.Equals(""))
                HandleExternalTileSelection();
            else
            {
                _selectedTile = 0;
                DllImports.SelectTile(_selectedTile, 0);
                ShowMenu(true);
            }
        }
        
        private Action HandleLoadingError(string message)
        {
            return async () =>
            {
                await DisplayAlert("Resources loading error", message, "OK");
                Exit();
            };
        }

        private void HandleExternalPlaybackStart()
        {
            if (Player != null) // it's possible that playback has already started via other control path (PreviewPayloadHandler vs HandleLoadingFinished calls order)
                return;

            if (_selectedTile >= _resourceLoader.TilesCount)
            {
                ShowMenu(true);
                return;
            }

            ShowMenu(false);
            KeyPressedMenuUpdate(); // Playback UI should be visible when starting playback from deep link
            HandlePlaybackStart();
        }

        private async Task DisplayAlert(string title, string body, string button)
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
            fixed (byte* titleBytes = ResourceLoader.GetBytes(title), bodyBytes =
                ResourceLoader.GetBytes(body), buttonBytes = ResourceLoader.GetBytes(button))
            {
                DllImports.ShowAlert(new DllImports.AlertData()
                {
                    title = titleBytes,
                    titleLen = title.Length,
                    body = bodyBytes,
                    bodyLen = body.Length,
                    button = buttonBytes,
                    buttonLen = button.Length
                });
            }

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
            _progressBarShown = !_isMenuShown && _resourceLoader.IsLoadingFinished;
            if (!_progressBarShown && _options.Visible)
                _options.Hide();
        }

        private void HandleKeyRight()
        {
            if (_isMenuShown)
            {
                if (_selectedTile < _resourceLoader.TilesCount - 1)
                    _selectedTile = (_selectedTile + 1) % _resourceLoader.TilesCount;
                DllImports.SelectTile(_selectedTile, 1);
            }
            else if (_options.Visible)
            {
                _options.ControlRight();
            }
            else if (_progressBarShown)
            {
                _seekLogic.SeekForward();
            }
        }

        private void HandleKeyLeft()
        {
            if (_isMenuShown)
            {
                if (_selectedTile > 0)
                    _selectedTile = (_selectedTile - 1 + _resourceLoader.TilesCount) % _resourceLoader.TilesCount;
                DllImports.SelectTile(_selectedTile, 1);
            }
            else if (_options.Visible)
            {
                _options.ControlLeft();
            }
            else if (_progressBarShown)
            {
                _seekLogic.SeekBackward();
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
                switch (Player.State)
                {
                    case Common.PlayerState.Playing:
                        Player?.Pause();
                        break;
                    case Common.PlayerState.Paused:
                        Player?.Start();
                        break;
                }
            }
            else if (_options.Visible && _options.ProperSelection())
            {
                _options.ControlSelect(Player);
                _options.Hide();
            }
        }

        private void HandlePlaybackStart()
        {
            if (Player == null)
            {
                Player = new Player(_playerWindow);
                Player.StateChanged()
                    .ObserveOn(SynchronizationContext.Current)
                    .Where(state => state == Common.PlayerState.Prepared)
                    .Subscribe(state =>
                    {
                        if (Player == null)
                            return;
                        _options.LoadStreamLists(Player);
                        Player.Start();
                    }, () => { _playbackCompletedNeedsHandling = true; });

                Player.PlaybackError()
                    .ObserveOn(SynchronizationContext.Current)
                    .Subscribe(message =>
                    {
                        Logger?.Info($"Playback Error: {message}");
                        ReturnToMainMenu();
                        DisplayAlert("Playback Error", message, "OK").ConfigureAwait(true);
                    });

                Player.BufferingProgress()
                    .ObserveOn(SynchronizationContext.Current)
                    .Subscribe(UpdateBufferingProgress);
            }

            Logger?.Info(
                $"Playing {_resourceLoader.ContentList[_selectedTile].Title} ({_resourceLoader.ContentList[_selectedTile].Url})");
            Player.SetSource(_resourceLoader.ContentList[_selectedTile]);
            _options.ClearOptionsMenu();
            SetSeekLogicAndSeekPreview();
            _bufferingInProgress = false;
            _bufferingProgress = 0;
        }

        private void SetSeekLogicAndSeekPreview()
        {
            _seekLogic.IsSeekInProgress = false;
            string previewPath = _resourceLoader.ContentList[_selectedTile].SeekPreviewPath;
            StoryboardReader seekPreviewReader = previewPath != null ? new StoryboardReader(previewPath) : null;
            StoryboardManager.GetInstance().SetSeekPreviewReader(seekPreviewReader, _seekLogic);
        }

        private void UpdateBufferingProgress(int percent)
        {
            _bufferingProgress = percent;
            _bufferingInProgress = percent < 100;
            Logger.Info($"Buffering {(_bufferingInProgress ? $"in progress: {percent}%" : "ended")}.");
        }

        private void ReturnToMainMenu()
        {
            _seekLogic.Reset();
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
            Player?.Stop();
            Player?.Dispose();
            Player = null;
        }

        private void HandleKeyExit()
        {
            ClosePlayer();
            Exit();
        }

        private void HandleKeyPlay()
        {
            if (_isMenuShown)
            {
                if (_selectedTile >= _resourceLoader.TilesCount)
                    return;
                ShowMenu(false);
                HandlePlaybackStart();
            }
            else
            {
                if (Player?.State == Common.PlayerState.Playing)
                    Player?.Pause();
                else
                    Player?.Start();
            }
        }

        private void HandleKeyPause()
        {
            Player?.Pause();
        }

        private void HandleKeyStop()
        {
            ReturnToMainMenu();
        }

        private void HandleKeyRewind()
        {
            _seekLogic.SeekBackward();
        }

        private void HandleKeySeekForward()
        {
            _seekLogic.SeekForward();
        }

        private void HandleKeyRed()
        {
            _metricsHandler.SwitchVisibility();
        }

        private void HandleKeyGreen()
        {
            GC.Collect();
        }

        private static void ResetPlaybackControls()
        {
            DllImports.UpdatePlaybackControls(new DllImports.PlaybackData());
        }

        private void ShowMenu(bool show)
        {
            if (show == _isMenuShown)
                return;

            if(!show)
                StoryboardManager.GetInstance().UnloadTilePreview();

            _isMenuShown = show;
            DllImports.ShowMenu(_isMenuShown ? 1 : 0);
        }

        private unsafe void UpdateSubtitles()
        {
            if (Player?.CurrentCueText != null && _options.SubtitlesOn)
            {
                fixed (byte* cueText = ResourceLoader.GetBytes(Player.CurrentCueText))
                    DllImports.ShowSubtitle(0, cueText,
                        Player.CurrentCueText.Length); // 0ms duration - special value just for next frame
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
            _seekLogic.Reset();
            ResetPlaybackControls();
            ShowMenu(true);
            ClosePlayer();
            _seekLogic.IsSeekAccumulationInProgress = false;
        }

        private void UpdateMetrics()
        {
            _metricsHandler.Update();
        }

        private static PlayerState ToPlayerState(Common.PlayerState state)
        {
            switch (state)
            {
                case Common.PlayerState.Idle:
                    return PlayerState.Idle;
                case Common.PlayerState.Prepared:
                    return PlayerState.Prepared;
                case Common.PlayerState.Paused:
                    return PlayerState.Paused;
                case Common.PlayerState.Playing:
                    return PlayerState.Playing;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private unsafe void UpdatePlaybackControls()
        {
            if (_progressBarShown && Player?.State == Common.PlayerState.Playing &&
                (DateTime.Now - _lastKeyPressTime).TotalMilliseconds >= _progressBarFadeout.TotalMilliseconds)
            {
                _progressBarShown = false;
                _options.Hide();
                Logger?.Info(
                    $"{(DateTime.Now - _lastKeyPressTime).TotalMilliseconds} ms of inactivity, hiding progress bar.");
            }

            if (!_resourceLoader.IsLoadingFinished)
                return;

            string title = _selectedTile > -1 ? _resourceLoader.ContentList[_selectedTile].Title : "";
            fixed (byte* name = ResourceLoader.GetBytes(title))
            {
                DllImports.UpdatePlaybackControls(new DllImports.PlaybackData()
                {
                    show = _progressBarShown ? 1 : 0,
                    state = (int)ToPlayerState(Player?.State ?? Common.PlayerState.Idle),
                    currentTime = (int)_seekLogic.CurrentPositionUI.TotalMilliseconds,
                    totalTime = (int)_seekLogic.Duration.TotalMilliseconds,
                    text = name,
                    textLen = title.Length,
                    buffering = _bufferingInProgress ? 1 : 0,
                    bufferingPercent = _bufferingProgress,
                    seeking = (_seekLogic.IsSeekInProgress || _seekLogic.IsSeekAccumulationInProgress) ? 1 : 0
                });
            }
        }

        private void UpdateUi()
        {
            UpdateSubtitles();
            UpdatePlaybackCompleted();
            UpdatePlaybackControls();
            UpdateMetrics();
        }
    }
}