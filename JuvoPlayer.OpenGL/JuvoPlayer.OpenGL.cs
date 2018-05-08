using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.OpenGL.Services;
using Tizen;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL {
    internal unsafe partial class Program : TVGLApplication
    {
        private static readonly Icon[] Icons = {
            new Icon {
                Id = IconType.Play,
                ImgPath = "play.png"
            },
            new Icon {
                Id = IconType.Resume,
                ImgPath = "resume.png"
            },
            new Icon {
                Id = IconType.Stop,
                ImgPath = "stop.png"
            },
            new Icon {
                Id = IconType.Pause,
                ImgPath = "pause.png"
            },
            new Icon {
                Id = IconType.FastForward,
                ImgPath = "fast-forward.png"
            },
            new Icon {
                Id = IconType.Rewind,
                ImgPath = "rewind.png"
            },
            new Icon {
                Id = IconType.SkipToEnd,
                ImgPath = "skip-to-end.png"
            },
            new Icon {
                Id = IconType.SkipToStart,
                ImgPath = "skip-to-start.png"
            }
        };

        private int _tilesNumber = 0;
        private int _tilesNumberTarget = 0;
        private Queue<Tile> _loadedTiles;

        private int _fontsNumber = 0;
        private readonly int _fontsNumberTarget = 1;
        private Queue<KeyValuePair<List<int>, byte[]>> _loadedFonts;

        private int _iconsNumber = 0;
        private readonly int _iconsNumberTarget = Icons.Length;
        private Queue<Icon> _loadedIcons;

        private int _selectedTile = 0;
        private bool _menuShown = true;

        private readonly OptionsMenu _options = new OptionsMenu();

        private bool _progressBarShown = false;
        private DateTime _lastAction = DateTime.Now;
        private readonly TimeSpan _prograssBarFadeout = TimeSpan.FromMilliseconds(7 * 1000);
        private readonly TimeSpan _defaultSeekTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _defaultSeekAccumulateTime = TimeSpan.FromSeconds(2);
        private TimeSpan _accumulatedSeekTime = TimeSpan.Zero;
        private Task _seekTask = null;

        PlayerService _player = null;
        private int _playerTimeCurrentPosition = 0;
        private int _playerTimeDuration = 0;
        private int _playerState = 0;
        private bool _handlePlaybackCompleted = false;

        private List<DetailContentData> ContentList { get; set; }
        private List<Clip> _clips;
        private string _cueText = "";

        private void InitMenu()
        {
            var clipsPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), "shared", "res", "videoclips.json");
            _clips = ClipReaderService.ReadClips(clipsPath);
            ContentList = _clips.Select(o => new DetailContentData() {
                Bg = o.Image,
                Clip = o.ClipDetailsHandle,
                ContentFocusedCommand = null,
                Description = o.Description,
                Image = o.Image,
                Source = o.Source,
                Title = o.Title,
            }).ToList();

            var home = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), "res/");
            _loadedFonts = new Queue<KeyValuePair<List<int>, byte[]>>();
            LoadFont(home + "fonts/akashi.ttf");
            _loadedTiles = new Queue<Tile>();
            _loadedIcons = new Queue<Icon>();
            _tilesNumberTarget = ContentList.Count;
            foreach (var contentItem in ContentList)
            {
                var tile = new Tile {
                    ImgPath = home + "tiles/" + contentItem.Image,
                    Description = contentItem.Description ?? "",
                    Name = contentItem.Title ?? ""
                };
                LoadTile(tile);
            }
            for(var i = 0; i < Icons.Length; ++i) {
                Icons[i].ImgPath = home + "icons/" + Icons[i].ImgPath;
                LoadIcon(Icons[i]);
            }
            SelectTile(_selectedTile);
            _selectedTile = 0;
            _menuShown = true;
            ShowLoader(1, 0);
            string footer = "JuvoPlayer AprilPrealpha, OpenGL UI #" + OpenGLLibVersion().ToString("x") + ", Samsung R&D Poland 2017-2018";
            fixed (byte* f = GetBytes(footer))
                SetFooter(f, footer.Length);
            //SwitchFPSCounterVisibility();
        }

        protected override void OnCreate()
        {
            Create();
            InitMenu();
        }

        private void OnRenderSubtitle(Subtitle subtitle) {
            //throw new NotImplementedException();
        }

        private void OnPlaybackCompleted() {
            //throw new NotImplementedException();
        }

        private void OnTimeUpdated(TimeSpan time) {
            //throw new NotImplementedException();
        }

        protected override void OnKeyEvent(Key key) {
            if (key.State != Key.StateType.Down)
                return;
            _lastAction = DateTime.Now;
            switch (key.KeyPressedName) {
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
                case "space":
                case "0":
                    _menuShown = !_menuShown;
                    ShowMenu(_menuShown ? 1 : 0);
                    break;
                case "1":
                    SwitchTextRenderingMode();
                    break;
                case "2":
                    SwitchFPSCounterVisibility();
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
                case "XF86AudioMute":
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
                case "XF86Red":
                case "XF86Green":
                case "XF86Yellow":
                case "XF86Blue":
                    break;
                default:
                    Log.Info("JuvoPlayer", "Unknown key pressed: " + key.KeyPressedName);
                    break;
            }
            _progressBarShown = !_menuShown;
            if(!_progressBarShown)
                _options.Hide();
        }

        private void UpdateUI() {
            if (_player != null && _player.CurrentCueText != null && _options.SubtitlesOn())
            {
                if (_cueText != _player.CurrentCueText)
                {
                    _cueText = _player.CurrentCueText;
                    Log.Info("JuvoPlayer", "CUE: " + _cueText);
                }
                fixed (byte* cueText = GetBytes(_player.CurrentCueText))
                    ShowSubtitle(0, cueText, _player.CurrentCueText.Length); // 0ms - just for next frame
            }

            if (_handlePlaybackCompleted) // doesn't work from side thread
            {
                _handlePlaybackCompleted = false;
                Log.Info("JuvoPlayer", "Playback completed. Returning to menu.");
                if (_menuShown)
                    return;
                _progressBarShown = false;
                _options.Hide();
                _menuShown = true;
                UpdatePlaybackControls(0, 0, 0, 0, null, 0);
                ShowMenu(_menuShown ? 1 : 0);
                if (_player != null) {
                    _player.Dispose(); // TODO: Check wheter it's the best way
                    _player = null;
                }
            }
            if(_player == null)
                _playerState = 0;
            _playerTimeCurrentPosition = (int)(_player?.CurrentPosition.TotalMilliseconds ?? 0);
            _playerTimeDuration = (int)(_player?.Duration.TotalMilliseconds ?? 0);
            if (_progressBarShown && _playerState == (int) PlayerState.Playing &&
                (DateTime.Now - _lastAction).TotalMilliseconds >= _prograssBarFadeout.TotalMilliseconds)
            {
                _progressBarShown = false;
                _options.Hide();
                Log.Info("JuvoPlayer", (DateTime.Now - _lastAction).TotalMilliseconds + "ms of inactivity, hiding progress bar.");
            }

            fixed (byte* name = GetBytes(ContentList[_selectedTile].Title))
                UpdatePlaybackControls(_progressBarShown ? 1 : 0, _playerState, _playerTimeCurrentPosition, _playerTimeDuration, name, ContentList[_selectedTile].Title.Length);
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            LoadResources();
            UpdateUI();
            Draw(eglDisplay, eglSurface);
        }

        private static void Main(string[] args)
        {
            var myProgram = new Program();
            myProgram.Run(args);
        }
    }
}