using System;
using System.IO;
using JuvoLogger;
using JuvoPlayer.OpenGL.Services;
using Tizen.TV.NUI.GLApplication;
using System.Linq;
using System.Threading.Tasks;
using Tizen.System;

// TODO: Separate parts of Program class code to individual classes as appropriate. https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=691
// TODO: Change "using StreamDescription = JuvoPlayer.OpenGL.Services.StreamDescription;" to JuvoPlayer.Common.StreamDescription in options menu (remove this Services part) https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=716
// TODO: Get rid of loading queues; Maybe create task for every item and later wait for all tasks to return results? But I still need to load resources to GPU from main thread, so instead of having queue of loaded resources we'd have queue of finished tasks containing loaded resources... https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=676

// TODO: Handle key name strings from other remotes https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=725
// TODO: Investigate a way to "properly" pass objects/structures containing containing byte arrays to native code https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=683
// TODO: Handle resource loading failure in a way that does not cause loading to loop indefinetely https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=688
// TODO: Make code styling consistent with the rest of the solution https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=694
// TODO: Find a way to refactor extern methods so passing a struct is possible instead of passing multiple arguments (solve byte* passing problem)
// TODO: Use JuvoPlayer.TizenTests.Utils.Paths class (move it to common) instead of Path.GetDirectoryName train wreck. https://bitbucket.sprc.samsung.pl/projects/PSW/repos/juvo-player/pull-requests/38/overview?commentId=721
// TODO: Decide on whether move all event handling logic to C++ library (so e.g. UI appearance change does not require change in both - C# and C++ - modules).

namespace JuvoPlayer.OpenGL
{
    internal class Program : TVGLApplication
    {
        private const bool LoadTestContentList = true;

        private readonly TimeSpan _prograssBarFadeout = TimeSpan.FromMilliseconds(5000);
        private readonly TimeSpan _defaultSeekTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _defaultSeekAccumulateTime = TimeSpan.FromMilliseconds(1000);

        private DateTime _lastKeyPressTime;
        private int _selectedTile;
        private bool _isMenuShown;
        private bool _progressBarShown;
        private bool _metricsShown;

        private PlayerService _player;
        private TimeSpan _playerTimeCurrentPosition;
        private TimeSpan _playerTimeDuration;
        private bool _playbackCompletedNeedsHandling;

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private OptionsMenu _options;
        private ResourceLoader _resourceLoader;

        private TimeSpan _accumulatedSeekTime;
        private DateTime _lastSeekTime;
        private bool _seekBufferingInProgress = false;
        private static readonly object _seekLock = new object();

        private SystemMemoryUsage _systemMemoryUsage = new SystemMemoryUsage();
        private int _systemMemoryUsageGraphId = -1;

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
            _resourceLoader.LoadResources(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), LoadTestContentList);
            SetupMetrics();
            SetMenuFooter();
            SetupOptionsMenu();
            SetDefaultMenuState();
        }

        private unsafe void SetupMetrics()
        {
            string tag = "MEM";
            fixed (byte* name = ResourceLoader.GetBytes(tag))
                _systemMemoryUsageGraphId = DllImports.AddGraph(name, tag.Length, 0, (float)_systemMemoryUsage.Total / 1024, 100);
            if (_systemMemoryUsageGraphId > DllImports.fpsGraphId)
                DllImports.SetGraphVisibility(_systemMemoryUsageGraphId, _metricsShown ? 1 : 0);
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

            _metricsShown = false;
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
            else if(key.KeyPressedName.Contains("XF86Back"))
            {
                HandleKeyBack();
            }
            else if(key.KeyPressedName.Contains("XF86Exit"))
            {
                HandleKeyExit();
            }
            else if(key.KeyPressedName.Contains("XF863XSpeed"))
            {
                HandleKeyPlay();
            }
            else if(key.KeyPressedName.Contains("XF86AudioPause"))
            {
                HandleKeyPause();
            }
            else if(key.KeyPressedName.Contains("XF863D"))
            {
                HandleKeyStop();
            }
            else if(key.KeyPressedName.Contains("XF86AudioRewind"))
            {
                HandleKeyRewind();
            }
            else if (key.KeyPressedName.Contains("XF86AudioNext"))
            {
                HandleKeySeekForward();
            }
            else if(key.KeyPressedName.Contains("XF86Red"))
            {
                _metricsShown = !_metricsShown;
                DllImports.SetGraphVisibility(DllImports.fpsGraphId, _metricsShown ? 1 : 0);
                if (_systemMemoryUsageGraphId > DllImports.fpsGraphId)
                    DllImports.SetGraphVisibility(_systemMemoryUsageGraphId, _metricsShown ? 1 : 0);
            }
            else if(key.KeyPressedName.Contains("XF86Green"))
            {
                ShowMenu(!_isMenuShown);
            }
            else if(key.KeyPressedName.Contains("XF86Yellow"))
            {
                DllImports.SwitchTextRenderingMode();
            }
            else if(key.KeyPressedName.Contains("XF86Blue"))
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
                _player = new PlayerService();
                _player.StateChanged += (object sender, PlayerStateChangedEventArgs e) =>
                {
                    Logger?.Info("Player state changed: " + _player.State);
                    if (_player.State == PlayerState.Prepared)
                        _player?.Start();
                    else if (_player.State == PlayerState.Completed)
                        _playbackCompletedNeedsHandling = true;
                };
            }

            Logger?.Info("Playing " + _resourceLoader.ContentList[_selectedTile].Title + " (" + _resourceLoader.ContentList[_selectedTile].Source + ")");
            _player.SetSource(_resourceLoader.ContentList[_selectedTile].Clip);
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
            UpdatePlaybackControls(); // TODO(g.skowinski): To fix: after seek command is sent, progress bar returns to current time and then after seek is executed, it jumps to correct time.
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

        private void UpdateMetrics()
        {
            if (_systemMemoryUsageGraphId > DllImports.fpsGraphId)
            {
                _systemMemoryUsage.Update();
                DllImports.UpdateGraphValue(_systemMemoryUsageGraphId, (float)_systemMemoryUsage.Used / 1024);
            }
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