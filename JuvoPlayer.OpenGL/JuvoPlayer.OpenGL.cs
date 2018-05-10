using System;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.OpenGL.Services;
using Tizen.TV.NUI.GLApplication;

// TODO: Refactor (+reclass?)
// TODO: Fix seek progress bar jump issue
// TODO: Decide what to do with resource loading failures
// TODO: Harden exception and error handling

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private const bool LoadTestContentList = true;

        private readonly TimeSpan _prograssBarFadeout = TimeSpan.FromMilliseconds(5000);
        private readonly TimeSpan _defaultSeekTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _defaultSeekAccumulateTime = TimeSpan.FromMilliseconds(1000);

        private DateTime _lastAction;
        private int _selectedTile;
        private bool _menuShown;
        private bool _progressBarShown;

        private PlayerService _player;
        private int _playerTimeCurrentPosition;
        private int _playerTimeDuration;
        private int _playerState;
        private bool _handlePlaybackCompleted;

        private ILogger _logger;

        private OptionsMenu _options;

        protected override void OnCreate()
        {
            Create();
            InitMenu();
        }

        private void InitMenu()
        {
            LoadResources();
            SetMenuFooter();
            SetupLogger();
            SetupOptionsMenu();
            SetDefaultMenuState();
        }

        private void SetMenuFooter()
        {
            var footer = "JuvoPlayer Prealpha, OpenGL UI #" + OpenGLLibVersion().ToString("x") +
                            ", Samsung R&D Poland 2017-2018";
            fixed (byte* f = GetBytes(footer))
                SetFooter(f, footer.Length);
        }

        private void SetDefaultMenuState()
        {
            SelectTile(_selectedTile);
            _selectedTile = 0;
            _menuShown = true;
            ShowLoader(1, 0);

            _lastAction = DateTime.Now;
            _accumulatedSeekTime = TimeSpan.Zero;
            _lastSeekTime = DateTime.MinValue;

            _playerTimeCurrentPosition = 0;
            _playerTimeDuration = 0;
            _playerState = (int)PlayerState.Idle;
            _handlePlaybackCompleted = false;
        }

        private void SetupLogger()
        {
            _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        }

        private void SetupOptionsMenu()
        {
            _options = new OptionsMenu
            {
                Logger = _logger
            };
        }

        protected override void OnKeyEvent(Key key)
        {
            if (key.State != Key.StateType.Down)
                return;

            switch (key.KeyPressedName)
            {
                case "Right":
                    HandleKeyRight();
                    break;
                case "Left":
                    HandleKeyLeft();
                    break;
                case "Up":
                    HandleKeyUp();
                    break;
                case "Down":
                    HandleKeyDown();
                    break;
                case "Return":
                    HandleKeyReturn();
                    break;
                case "XF86Back":
                    HandleKeyBack();
                    break;
                case "XF86Exit":
                    HandleKeyExit();
                    break;
                case "XF863XSpeed":
                    HandleKeyPlay();
                    break;
                case "XF86AudioPause":
                    HandleKeyPause();
                    break;
                case "XF863D":
                    HandleKeyStop();
                    break;
                case "XF86AudioRewind":
                    HandleKeyRewind();
                    break;
                case "XF86AudioNext":
                    HandleKeySeekForward();
                    break;
                case "XF86Info":
                    SwitchFPSCounterVisibility();
                    break;
                case "XF86Red":
                    SwitchFPSCounterVisibility();
                    break;
                case "XF86Green":
                    _menuShown = !_menuShown;
                    ShowMenu(_menuShown ? 1 : 0);
                    break;
                case "XF86Yellow":
                    SwitchTextRenderingMode();
                    break;
                case "XF86Blue":
                    break;
                default:
                    _logger?.Info("Unknown key pressed: " + key.KeyPressedName);
                    break;
            }

            KeyPressedMenuUpdate();
        }

        private void KeyPressedMenuUpdate()
        {
            _lastAction = DateTime.Now;
            _progressBarShown = !_menuShown;
            if (!_progressBarShown && _options.IsShown())
                _options.Hide();
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            LoadQueuedResources();
            UpdateUI();
            Draw(eglDisplay, eglSurface);
        }

        private void OnRenderSubtitle(Subtitle subtitle)
        {
            //throw new NotImplementedException();
        }

        private void OnPlaybackCompleted()
        {
            //throw new NotImplementedException();
        }

        private void OnTimeUpdated(TimeSpan time)
        {
            //throw new NotImplementedException();
        }

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }
    }
}