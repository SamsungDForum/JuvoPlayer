using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImageSharp;
using JuvoPlayer.Common;
using JuvoPlayer.OpenGL.Services;
using Tizen;
using Tizen.TV.NUI.GLApplication;
using StreamDescription = JuvoPlayer.OpenGL.Services.StreamDescription;

namespace JuvoPlayer.OpenGL {
    internal unsafe class Program : TVGLApplication
    {
        private const string GlDemoLib = "libgles_sample.so";

        [DllImport(GlDemoLib, EntryPoint = "Create")]
        public static extern void Create();

        [DllImport(GlDemoLib, EntryPoint = "Draw")]
        public static extern void Draw(IntPtr eglDisplay, IntPtr eglSurface);

        [DllImport(GlDemoLib, EntryPoint = "AddTile")]
        public static extern int AddTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileData")]
        public static extern void SetTileData(int tileId, byte* pixels, int w, int h, byte *name, int nameLen, byte *desc, int descLen);

        [DllImport(GlDemoLib, EntryPoint = "AddEmptyTile")]
        public static extern int AddEmptyTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileTexture")]
        public static extern int SetTileTexture(int tileNo, byte* pixels, int w, int h);

        [DllImport(GlDemoLib, EntryPoint = "SelectTile")]
        public static extern void SelectTile(int tileNo);

        [DllImport(GlDemoLib, EntryPoint = "ShowMenu")]
        public static extern void ShowMenu(int enable);

        [DllImport(GlDemoLib, EntryPoint = "AddFont")]
        public static extern int AddFont(byte* data, int size);

        [DllImport(GlDemoLib, EntryPoint = "ShowLoader")]
        public static extern void ShowLoader(int enabled, int percent);

        [DllImport(GlDemoLib, EntryPoint = "UpdatePlaybackControls")]
        public static extern void UpdatePlaybackControls(int show, int state, int currentTime, int totalTime, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "SetIcon")]
        public static extern void SetIcon(int id, byte* pixels, int w, int h);

        [DllImport(GlDemoLib, EntryPoint = "SetFooter")]
        public static extern void SetFooter(byte* footer, int footerLen);

        [DllImport(GlDemoLib, EntryPoint = "SwitchTextRenderingMode")]
        public static extern void SwitchTextRenderingMode();

        [DllImport(GlDemoLib, EntryPoint = "SwitchFPSCounterVisibility")]
        public static extern void SwitchFPSCounterVisibility();

        [DllImport(GlDemoLib, EntryPoint = "ShowSubtitle")]
        public static extern void ShowSubtitle(int duration, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "OpenGLLibVersion")]
        public static extern int OpenGLLibVersion();

        [DllImport(GlDemoLib, EntryPoint = "AddOption")]
        public static extern int AddOption(int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "AddSuboption")]
        public static extern int AddSuboption(int parentId, int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "UpdateSelection")]
        public static extern int UpdateSelection(int show, int activeOptionId, int activeSuboptionId, int selectedOptionId, int selectedSuboptionId);

        [DllImport(GlDemoLib, EntryPoint = "ClearOptions")]
        public static extern void ClearOptions();


        private struct Tile {
            public int Id;
            public string ImgPath;
            public int ImgWidth;
            public int ImgHeight;
            public byte[] ImgPixels;
            public string Name;
            public string Description;
        }

        private enum IconType {
            Play,
            Resume,
            Stop,
            Pause,
            FastForward,
            Rewind,
            SkipToEnd,
            SkipToStart
        };

        private struct Icon {
            public IconType Id;
            public string ImgPath;
            public int ImgWidth;
            public int ImgHeight;
            public byte[] ImgPixels;
        }

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
        private int _fontsNumberTarget = 1;
        private Queue<KeyValuePair<List<int>, byte[]>> _loadedFonts;

        private int _iconsNumber = 0;
        private int _iconsNumberTarget = Icons.Length;
        private Queue<Icon> _loadedIcons;

        private int _selectedTile = 0;
        private bool _menuShown = true;

        private class OptionsMenu
        {
            private bool _optionsShown = false;
            private int _activeOption = -1;
            private int _activeSuboption = -1;
            private int _selectedOption = -1;
            private int _selectedSuboption = -1;
            private bool _subtitlesOn = false;

            private class Stream
            {
                public List<Services.StreamDescription> descriptions;
                public StreamDescription.StreamType streamType;
                public string title;
                public int active;
                public int id;
            };

            private List<Stream> streams = new List<Stream>();

            public void GetStreams(PlayerService player) {
                ClearOptionsMenu();
                if (player != null) {
                    foreach(var streamType in new[] { StreamDescription.StreamType.Video, StreamDescription.StreamType.Audio, StreamDescription.StreamType.Subtitle})
                    {
                        var stream = new Stream();
                        stream.id = streams.Count;
                        stream.streamType = streamType;
                        stream.title = streamType.ToString();
                        if (streamType == StreamDescription.StreamType.Subtitle) {
                            stream.descriptions = new List<StreamDescription>
                            {
                                new StreamDescription()
                                {
                                    Default = true,
                                    Description = "off",
                                    Id = 0,
                                    Type = StreamDescription.StreamType.Subtitle
                                }
                            };
                            _subtitlesOn = false;
                        }
                        else
                        {
                            stream.descriptions = new List<StreamDescription>();
                        }
                        stream.descriptions.AddRange(player.GetStreamsDescription(streamType));
                        stream.active = -1;
                        fixed (byte* text = GetBytes(stream.title))
                            AddOption(stream.id, text, stream.title.Length);
                        for(int id = 0; id < stream.descriptions.Count; ++id)
                        {
                            var s = stream.descriptions[id];
                            Log.Info("JuvoPlayer", "stream.Description=\"" + s.Description + "\", stream.Id=\"" + s.Id + "\", stream.Type=\"" + s.Type + "\", stream.Default=\"" + s.Default + "\"");
                            if (s.Default)
                            {
                                stream.active = id;
                                if (streamType == StreamDescription.StreamType.Subtitle)
                                    _subtitlesOn = true;
                            }
                            fixed (byte* text = GetBytes(s.Description))
                                AddSuboption(stream.id, id, text, s.Description.Length);
                        }
                        streams.Add(stream);
                    }
                    _activeOption = -1;
                    _activeSuboption = -1;
                    _selectedOption = 0;
                    _selectedSuboption = -1;
                    UpdateOptionsSelection();
                }
            }

            private void UpdateOptionsSelection() {
                Log.Info("JuvoPlayer", "activeOption=" + _activeOption + ", activeSuboption=" + _activeSuboption + ", selectedOption=" + _selectedOption + ", selectedSuboption=" + _selectedSuboption);
                if(_selectedOption >= 0 && _selectedOption < streams.Count)
                    _activeSuboption = streams[_selectedOption].active;
                UpdateSelection(_optionsShown ? 1 : 0, _activeOption, _activeSuboption, _selectedOption, _selectedSuboption);
            }

            private void ClearOptionsMenu() {
                _activeOption = -1;
                _activeSuboption = -1;
                _selectedOption = -1;
                _selectedSuboption = -1;
                streams = new List<Stream>();
                ClearOptions();
            }
            public void ControlLeft()
            {
                if(_selectedSuboption == -1)
                    Hide();
                else
                {
                    _selectedSuboption = -1;
                    UpdateOptionsSelection();
                }
            }

            public void ControlRight()
            {
                if (_selectedSuboption == -1 && streams[_selectedOption].descriptions.Count > 0)
                    _selectedSuboption = streams[_selectedOption].descriptions.Count - 1;
                UpdateOptionsSelection();
            }

            public void ControlUp()
            {
                if (_selectedSuboption == -1) {
                    if (_selectedOption > 0)
                        --_selectedOption;
                }
                else {
                    if (_selectedSuboption > 0)
                        --_selectedSuboption;
                }
                UpdateOptionsSelection();
            }

            public void ControlDown()
            {
                if (_selectedSuboption == -1) {
                    if (_selectedOption < streams.Count - 1)
                        ++_selectedOption;
                }
                else {
                    if (_selectedSuboption < streams[_selectedOption].descriptions.Count - 1)
                        ++_selectedSuboption;
                }
                UpdateOptionsSelection();
            }

            public void ControlSelect(PlayerService player)
            {
                if (_selectedSuboption >= 0 && _selectedSuboption < streams[_selectedOption].descriptions.Count)
                {
                    if (streams[_selectedOption].descriptions[_selectedSuboption].Type == StreamDescription.StreamType.Subtitle && _selectedSuboption == 0) // turn off subtitles
                    {
                        // TODO(g.skowinski): Turn off subtitles
                        _subtitlesOn = false;
                    }
                    else
                    {
                        if (streams[_selectedOption].descriptions[_selectedSuboption].Type == StreamDescription.StreamType.Subtitle)
                            _subtitlesOn = true;
                        player.ChangeActiveStream(streams[_selectedOption].descriptions[_selectedSuboption]);
                    }
                    streams[_selectedOption].active = _selectedSuboption;
                    _activeSuboption = _selectedSuboption;
                }
                UpdateOptionsSelection();
            }

            public void Show()
            {
                _selectedOption = streams.Count - 1;
                _selectedSuboption = -1;
                _optionsShown = true;
                UpdateOptionsSelection();
            }

            public void Hide()
            {
                _optionsShown = false;
                UpdateOptionsSelection();
            }

            public bool IsShown()
            {
                return _optionsShown;
            }

            public bool SubtitlesOn()
            {
                return _subtitlesOn;
            }
        }

        private OptionsMenu _options = new OptionsMenu();

        private bool _progressBarShown = false;
        private DateTime _lastAction = DateTime.Now;
        private TimeSpan _prograssBarFadeout = TimeSpan.FromMilliseconds(7 * 1000);
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

        private void LoadTile(Tile tile) {
            tile.Id = AddTile();
            Task.Run(() => {
                try {
                    using (var stream = File.OpenRead(tile.ImgPath)) {
                        var image = new Image(stream);
                        var pixels = new byte[image.Pixels.Length * 3];
                        for (var i = 0; i < image.Pixels.Length; ++i) {
                            pixels[3 * i + 0] = image.Pixels[i].R;
                            pixels[3 * i + 1] = image.Pixels[i].G;
                            pixels[3 * i + 2] = image.Pixels[i].B;
                        }
                        Log.Info("JuvoPlayer", tile.ImgPath + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                        tile.ImgWidth = image.Width;
                        tile.ImgHeight = image.Height;
                        tile.ImgPixels = pixels;
                        lock (_loadedTiles) {
                            _loadedTiles.Enqueue(tile);
                        }
                    }
                }
                catch (Exception e) {
                    Log.Info("JuvoPlayer", e.ToString());
                }
            });
        }

        private void LoadFont(string file) {
            Task.Run(() => {
                try {
                    using (var stream = File.OpenRead(file)) {
                        var font = new byte[stream.Length];
                        stream.Read(font, 0, (int)stream.Length);
                        lock(_loadedFonts) {
                            _loadedFonts.Enqueue(new KeyValuePair<List<int>, byte[]>(new List<int>(new int[] {}), font));
                        }
                    }
                }
                catch (Exception e) {
                    Log.Info("JuvoPlayer", e.ToString());
                }
            });
        }

        private void LoadIcon(Icon icon) {
            Task.Run(() => {
                try {
                    using (var stream = File.OpenRead(icon.ImgPath)) {
                        var image = new Image(stream);
                        var pixels = new byte[image.Pixels.Length * 4];
                        for (var i = 0; i < image.Pixels.Length; ++i) {
                            pixels[4 * i + 0] = image.Pixels[i].R;
                            pixels[4 * i + 1] = image.Pixels[i].G;
                            pixels[4 * i + 2] = image.Pixels[i].B;
                            pixels[4 * i + 3] = image.Pixels[i].A;
                        }
                        Log.Info("JuvoPlayer", icon.ImgPath + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                        icon.ImgWidth = image.Width;
                        icon.ImgHeight = image.Height;
                        icon.ImgPixels = pixels;
                        lock (_loadedTiles) {
                            _loadedIcons.Enqueue(icon);
                        }
                    }
                }
                catch (Exception e) {
                    Log.Info("JuvoPlayer", e.ToString());
                }
            });
        }

        private static byte[] GetBytes(string str) {
            var a = new byte[str.Length];
            for (var i = 0; i < str.Length; ++i)
                a[i] = (byte)str[i];
            return a;
        }

        private void LoadResources() {
            var resourcesTarget = _tilesNumberTarget + _fontsNumberTarget + _iconsNumberTarget;
            var resourcesLoaded = _tilesNumber + _fontsNumber + _iconsNumber;
            if (resourcesLoaded >= resourcesTarget)
                return;
            lock (_loadedTiles) {
                while (_loadedTiles.Count > 0) {
                    try {
                        var tile = _loadedTiles.Dequeue();
                        fixed (byte* p = tile.ImgPixels) {
                            fixed (byte* name = GetBytes(tile.Name)) {
                                fixed (byte* desc = GetBytes(tile.Description)) {
                                    SetTileData(tile.Id, p, tile.ImgWidth, tile.ImgHeight, name, tile.Name.Length, desc, tile.Description.Length);
                                }
                            }
                        }
                        ++_tilesNumber;
                        UpdateLoader();
                    }
                    catch (Exception e) {
                        Log.Info("JuvoPlayer", e.ToString());
                    }
                }
            }
            lock(_loadedFonts) {
                while (_loadedFonts.Count > 0) {
                    try {
                        var font = _loadedFonts.Dequeue();
                        fixed (byte* p = font.Value) {
                            AddFont(p, font.Value.Length);
                        }
                        ++_fontsNumber;
                        UpdateLoader();
                    }
                    catch (Exception e) {
                        Log.Info("JuvoPlayer", e.ToString());
                    }
                }
            }
            lock (_loadedIcons) {
                while (_loadedIcons.Count > 0) {
                    try {
                        var icon = _loadedIcons.Dequeue();
                        fixed (byte* p = icon.ImgPixels) {
                            SetIcon((int)icon.Id, p, icon.ImgWidth, icon.ImgHeight);
                        }
                        ++_iconsNumber;
                        UpdateLoader();
                    }
                    catch (Exception e) {
                        Log.Info("JuvoPlayer", e.ToString());
                    }
                }
            }
        }

        private void UpdateLoader() {
            var resourcesTarget = _tilesNumberTarget + _fontsNumberTarget + _iconsNumberTarget;
            var resourcesLoaded = _tilesNumber + _fontsNumber + _iconsNumber;
            ShowLoader(resourcesLoaded < resourcesTarget ? 1 : 0, resourcesTarget > 0 ? 100 * resourcesLoaded / resourcesTarget : 0);
        }

        protected override void OnKeyEvent(Key key) {
            if (key.State != Key.StateType.Down)
                return;
            _lastAction = DateTime.Now;
            switch (key.KeyPressedName) {
                case "Right":
                    if (_menuShown)
                    {
                        if (_selectedTile < _tilesNumber - 1)
                            _selectedTile = (_selectedTile + 1) % _tilesNumber;
                        SelectTile(_selectedTile);
                    }
                    else if (_options.IsShown())
                    {
                        _options.ControlRight();
                    }
                    else if(_progressBarShown)
                    {
                        _options.Show();
                    }
                    break;
                case "Left":
                    if (_menuShown)
                    {
                        if (_selectedTile > 0)
                            _selectedTile = (_selectedTile - 1 + _tilesNumber) % _tilesNumber;
                        SelectTile(_selectedTile);
                    }
                    else if (_options.IsShown())
                    {
                        _options.ControlLeft();
                    }
                    break;
                case "Up":
                    if (!_menuShown && _options.IsShown()) {
                        _options.ControlUp();
                    }
                    break;
                case "Down":
                    if (!_menuShown && _options.IsShown()) {
                        _options.ControlDown();
                    }
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
                    if (_menuShown)
                    {
                        if (_selectedTile >= ContentList.Count)
                            return;
                        if (_player == null)
                            _player = new PlayerService();
                        _player.PlaybackCompleted += () => { _handlePlaybackCompleted = true; };
                        _player.StateChanged += (object sender, PlayerStateChangedEventArgs e) =>
                        {
                            Log.Info("JuvoPlayer", "Player state changed: " + _player.State);
                            _playerState = (int) _player.State;
                            if (_player.State == PlayerState.Prepared)
                                _player?.Start();
                        };
                        Log.Info("JuvoPlayer",
                            "Playing " + ContentList[_selectedTile].Title + " (" + ContentList[_selectedTile].Source +
                            ")");
                        _player.SetSource(ContentList[_selectedTile].Clip);
                        _options.GetStreams(_player);
                        if (!_menuShown)
                            break;
                        _menuShown = false;
                        ShowMenu(_menuShown ? 1 : 0);
                    }
                    else if (_options.IsShown())
                    {
                        _options.ControlSelect(_player);
                    }
                    else if (_progressBarShown)
                    {
                        _options.Show();
                    }
                    break;
                case "XF86Back":
                    if (!_menuShown && !_options.IsShown())
                    {
                        UpdatePlaybackControls(0, 0, 0, 0, null, 0);
                        _menuShown = true;
                        ShowMenu(_menuShown ? 1 : 0);
                        if (_player != null)
                        {
                            _player.Dispose(); // TODO: Check wheter it's the best way
                            _player = null;
                        }
                    }
                    else if (_options.IsShown())
                    {
                        _options.Hide();
                    }
                    break;
                case "XF86Exit":
                    Exit();
                    break;
                case "XF86AudioMute": // Mute
                case "XF863XSpeed": // Play
                    _player?.Start();
                    break;
                case "XF86AudioPause": // Pause
                    _player?.Pause();
                    break;
                case "XF863D": // Stop
                    _player?.Stop();
                    break;
                case "XF86AudioRewind": // Rewind
                    Seek(-_defaultSeekTime);
                    break;
                case "XF86AudioNext": // Seek forward
                    Seek(_defaultSeekTime);
                    break;
                case "XF86Info": // Info
                case "XF86Red": // A
                case "XF86Green": // B
                case "XF86Yellow": // C
                case "XF86Blue": // D
                    break;
                default:
                    Log.Info("JuvoPlayer", "Unknown key pressed: " + key.KeyPressedName);
                    break;
            }
            _progressBarShown = !_menuShown;
            if(!_progressBarShown)
                _options.Hide();
            //else
            //    _options.Show();
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
                    ShowSubtitle(0, cueText, _player.CurrentCueText.Length); // 0ms - just for next frame...
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
            _playerTimeCurrentPosition = (int)(_player != null ? _player.CurrentPosition.TotalMilliseconds : 0);
            _playerTimeDuration = (int)(_player != null ? _player.Duration.TotalMilliseconds : 0);
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

        private void Seek(TimeSpan seekTime)
        {
            if (_player != null) {
                Log.Info("JuvoPlayer", "Accumulating seek time " + _accumulatedSeekTime + " + " + seekTime);
                if (_seekTask == null)
                {
                    _accumulatedSeekTime = seekTime;
                    _seekTask = Task.Delay(_defaultSeekAccumulateTime).ContinueWith(_ =>
                    {
                        Log.Info("JuvoPlayer", "Seeking " + _accumulatedSeekTime.ToString());
                        _seekTask = null;
                        if (_accumulatedSeekTime > TimeSpan.Zero)
                            Forward(_accumulatedSeekTime);
                        else
                            Rewind(-_accumulatedSeekTime);
                    });
                }
                else
                {
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