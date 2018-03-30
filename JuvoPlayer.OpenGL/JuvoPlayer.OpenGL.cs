using System;
using System.IO;
using System.Runtime.InteropServices;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using ImageSharp;
using Tizen.System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Tizen.Applications;

using JuvoPlayer.OpenGL.Services;
using JuvoPlayer.Common;
using System.Linq;
using System.Windows.Input;

namespace Tizen.TV.NUI.GLApplication.JuvoPlayer.OpenGL
{
    internal unsafe class Program : TVGLApplication
    {
        private const string glDemoLib = "libgles_sample.so";

        [DllImport(glDemoLib, EntryPoint = "Create")]
        public static extern void Create();

        [DllImport(glDemoLib, EntryPoint = "Draw")]
        public static extern void Draw(IntPtr eglDisplay, IntPtr eglSurface);

        [DllImport(glDemoLib, EntryPoint = "AddTile")]
        public static extern int AddTile();

        [DllImport(glDemoLib, EntryPoint = "SetTileData")]
        public static extern void SetTileData(int tileId, byte* pixels, int w, int h, byte *name, int nameLen, byte *desc, int descLen);

        [DllImport(glDemoLib, EntryPoint = "AddEmptyTile")]
        public static extern int AddEmptyTile();

        [DllImport(glDemoLib, EntryPoint = "SetTileTexture")]
        public static extern int SetTileTexture(int tileNo, byte* pixels, int w, int h);

        [DllImport(glDemoLib, EntryPoint = "SelectTile")]
        public static extern void SelectTile(int tileNo);

        [DllImport(glDemoLib, EntryPoint = "ShowMenu")]
        public static extern void ShowMenu(int enable);

        [DllImport(glDemoLib, EntryPoint = "AddFont")]
        public static extern int AddFont(byte* data, int size);

        [DllImport(glDemoLib, EntryPoint = "ShowLoader")]
        public static extern void ShowLoader(int enabled, int percent);

        [DllImport(glDemoLib, EntryPoint = "UpdatePlaybackControls")]
        public static extern void UpdatePlaybackControls(int show, int state, int currentTime, int totalTime, byte* text, int textLen);

        [DllImport(glDemoLib, EntryPoint = "SetIcon")]
        public static extern void SetIcon(int id, byte* pixels, int w, int h);

        [DllImport(glDemoLib, EntryPoint = "SetVersion")]
        public static extern void SetVersion(byte* ver, int verLen);

        [DllImport(glDemoLib, EntryPoint = "SwitchTextRenderingMode")]
        public static extern void SwitchTextRenderingMode();

        [DllImport(glDemoLib, EntryPoint = "SwitchFPSCounterVisibility")]
        public static extern void SwitchFPSCounterVisibility();

        private struct Tile {
            public int id;
            public string imgPath;
            public int imgWidth;
            public int imgHeight;
            public byte[] imgPixels;
            public string name;
            public string description;
        }

        static private Tile[] tiles = {
            new Tile {
                name = "Blueprint",
                description = "A blueprint from a scene with a blueprint.",
                imgPath = "0.jpg"
            },
            new Tile {
                name = "Bay City",
                description = "Formerly known as San Francisco.\n\nRoses are FF0000\nViolets are 0000FF\nFont metrics handling sucks\nSo I deal with it with hacks",
                imgPath = "1.jpg"
            },
            new Tile {
                name = "Envoys",
                description = "Two Envoys in an industrial setting.",
                imgPath = "2.jpg"
            },
            new Tile {
                name = "Takeshi Kovacs",
                description = "The main character looking at Bay City.",
                imgPath = "3.jpg"
            },
            new Tile {
                name = "Neural Hologram",
                description = "Heavy sci-fi stuff.",
                imgPath = "4.jpg"
            },
            new Tile {
                name = "Born Again",
                description = "Takeshi Kovacs is being fit into new sleeve.",
                imgPath = "5.jpg"
            },
            new Tile {
                name = "Flashback",
                description = "Kovacs having an usual Envoy-in-a-forest flashback.",
                imgPath = "6.jpg"
            },
            new Tile {
                name = "Drugs",
                description = "Women and Rock&Roll in Bay City.",
                imgPath = "7.jpg"
            },
            new Tile {
                name = "Forest",
                description = "Envoys' training scene.",
                imgPath = "8.jpg"
            },
            new Tile {
                name = "The Russian",
                description = "Soon very dead Russian. Brother of another soon very dead Russian.",
                imgPath = "9.jpg"
            }
        };

        enum Icons {
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
            public Icons id;
            public string imgPath;
            public int imgWidth;
            public int imgHeight;
            public byte[] imgPixels;
        }

        static private Icon[] icons = {
            new Icon {
                id = Icons.Play,
                imgPath = "play.png"
            },
            new Icon {
                id = Icons.Resume,
                imgPath = "resume.png"
            },
            new Icon {
                id = Icons.Stop,
                imgPath = "stop.png"
            },
            new Icon {
                id = Icons.Pause,
                imgPath = "pause.png"
            },
            new Icon {
                id = Icons.FastForward,
                imgPath = "fast-forward.png"
            },
            new Icon {
                id = Icons.Rewind,
                imgPath = "rewind.png"
            },
            new Icon {
                id = Icons.SkipToEnd,
                imgPath = "skip-to-end.png"
            },
            new Icon {
                id = Icons.SkipToStart,
                imgPath = "skip-to-start.png"
            }
        };

        private int tilesNumber = 0;
        private int tilesNumberTarget = tiles.Length;
        private Queue<Tile> loadedTiles;

        private int fontsNumber = 0;
        private int fontsNumberTarget = 1;
        private Queue<KeyValuePair<List<int>, byte[]>> loadedFonts;

        private int iconsNumber = 0;
        private int iconsNumberTarget = icons.Length;
        private Queue<Icon> loadedIcons;

        private int selectedTile = 0;
        private int menuShown = 1;

        private int progressBarShown = 0;
        private DateTime lastAction = DateTime.Now;
        private TimeSpan prograssBarFadeout = TimeSpan.FromMilliseconds(5 * 1000);

        PlayerService player = null;
        private int playerTimeCurrentPosition = 0;
        private int playerTimeDuration = 0;
        private int playerState = 0;

        private List<DetailContentData> contentList { get; set; }
        private List<Clip> clips;

        protected ICommand CreateFocusedCommand() {
            return null;
        }

        private void InitMenu()
        {
            var clipsPath = global::System.IO.Path.Combine(global::System.IO.Path.GetDirectoryName(global::System.IO.Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), "shared", "res", "videoclips.json");
            clips = ClipReaderService.ReadClips(clipsPath);

            contentList = clips.Select(o => new DetailContentData() {
                Bg = o.Image,
                Clip = o.ClipDetailsHandle,
                ContentFocusedCommand = CreateFocusedCommand(),
                Description = o.Description,
                Image = o.Image,
                Source = o.Source,
                Title = o.Title,
            }).ToList();


            string path = global::System.IO.Path.Combine(global::System.IO.Path.GetDirectoryName(global::System.IO.Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), "res/");
            string home = "/home/owner/JuvoGL/";
            home = path;
            loadedFonts = new Queue<KeyValuePair<List<int>, byte[]>>();
            LoadFont(home + "fonts/akashi.ttf");
            loadedTiles = new Queue<Tile>();
            loadedIcons = new Queue<Icon>();
            tilesNumberTarget = contentList.Count;
            for (int i = 0; i < contentList.Count; ++i) {
                Tile tile = new Tile {
                    imgPath = home + "tiles/" + contentList[i].Image,
                    description = contentList[i].Description ?? "",
                    name = contentList[i].Title ?? ""
                };
                LoadTile(tile);
            }
            for(int i = 0; i < icons.Length; ++i) {
                icons[i].imgPath = home + "icons/" + icons[i].imgPath;
                LoadIcon(icons[i]);
            }
            SelectTile(selectedTile);
            selectedTile = 0;
            menuShown = 1;
            ShowLoader(1, 0);
            // ShowMenu(menuShown); // loader takes care of it
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

        void LoadTile(Tile tile) {
            tile.id = AddTile();
            Task.Run(() => {
                try {
                    using (FileStream stream = File.OpenRead(tile.imgPath)) {
                        Image image = new Image(stream);
                        byte[] pixels = new byte[image.Pixels.Length * 3];
                        for (int i = 0; i < image.Pixels.Length; ++i) {
                            pixels[3 * i + 0] = image.Pixels[i].R;
                            pixels[3 * i + 1] = image.Pixels[i].G;
                            pixels[3 * i + 2] = image.Pixels[i].B;
                        }
                        Log.Info("JuvoGL", tile.imgPath + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                        tile.imgWidth = image.Width;
                        tile.imgHeight = image.Height;
                        tile.imgPixels = pixels;
                        lock (loadedTiles) {
                            loadedTiles.Enqueue(tile);
                        }
                    }
                }
                catch (Exception e) {
                    Log.Info("JuvoGL", e.ToString());
                }
            });
        }

        void LoadFont(string file) {
            Task.Run(() => {
                try {
                    using (FileStream stream = File.OpenRead(file)) {
                        byte[] font = new byte[stream.Length];
                        stream.Read(font, 0, (int)stream.Length);
                        lock(loadedFonts) {
                            loadedFonts.Enqueue(new KeyValuePair<List<int>, byte[]>(new List<int>(new int[] {}), font));
                        }
                    }
                }
                catch (Exception e) {
                    Log.Info("JuvoGL", e.ToString());
                }
            });
        }

        void LoadIcon(Icon icon) {
            Task.Run(() => {
                try {
                    using (FileStream stream = File.OpenRead(icon.imgPath)) {
                        Image image = new Image(stream);
                        byte[] pixels = new byte[image.Pixels.Length * 4];
                        for (int i = 0; i < image.Pixels.Length; ++i) {
                            pixels[4 * i + 0] = image.Pixels[i].R;
                            pixels[4 * i + 1] = image.Pixels[i].G;
                            pixels[4 * i + 2] = image.Pixels[i].B;
                            pixels[4 * i + 3] = image.Pixels[i].A;
                        }
                        Log.Info("JuvoGL", icon.imgPath + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                        icon.imgWidth = image.Width;
                        icon.imgHeight = image.Height;
                        icon.imgPixels = pixels;
                        lock (loadedTiles) {
                            loadedIcons.Enqueue(icon);
                        }
                    }
                }
                catch (Exception e) {
                    Log.Info("JuvoGL", e.ToString());
                }
            });
        }

        byte[] GetBytes(string str) {
            byte[] a = new byte[str.Length];
            for (int i = 0; i < str.Length; ++i)
                a[i] = (byte)str[i];
            return a;
        }

        void LoadResources() {
            int resourcesTarget = tilesNumberTarget + fontsNumberTarget + iconsNumberTarget;
            int resourcesLoaded = tilesNumber + fontsNumber + iconsNumber;
            if (resourcesLoaded >= resourcesTarget)
                return;
            lock (loadedTiles) {
                while (loadedTiles.Count > 0) {
                    try {
                        Tile tile = loadedTiles.Dequeue();
                        fixed (byte* p = tile.imgPixels) {
                            fixed (byte* name = GetBytes(tile.name)) {
                                fixed (byte* desc = GetBytes(tile.description)) {
                                    SetTileData(tile.id, p, tile.imgWidth, tile.imgHeight, name, tile.name.Length, desc, tile.description.Length);
                                }
                            }
                        }
                        ++tilesNumber;
                        UpdateLoader();
                    }
                    catch (Exception e) {
                        Log.Info("JuvoGL", e.ToString());
                    }
                }
            }
            lock(loadedFonts) {
                while (loadedFonts.Count > 0) {
                    try {
                        KeyValuePair<List<int>, byte[]> font = loadedFonts.Dequeue();
                        fixed (byte* p = font.Value) {
                            int fid = AddFont(p, font.Value.Length);
                            Log.Info("JuvoGL", "FontID=" + fid);
                        }
                        ++fontsNumber;
                        UpdateLoader();
                    }
                    catch (Exception e) {
                        Log.Info("JuvoGL", e.ToString());
                    }
                }
            }
            lock (loadedIcons) {
                while (loadedIcons.Count > 0) {
                    try {
                        Icon icon = loadedIcons.Dequeue();
                        fixed (byte* p = icon.imgPixels) {
                            SetIcon((int)icon.id, p, icon.imgWidth, icon.imgHeight);
                        }
                        ++iconsNumber;
                        UpdateLoader();
                    }
                    catch (Exception e) {
                        Log.Info("JuvoGL", e.ToString());
                    }
                }
            }
        }

        void UpdateLoader() {
            int resourcesTarget = tilesNumberTarget + fontsNumberTarget + iconsNumberTarget;
            int resourcesLoaded = tilesNumber + fontsNumber + iconsNumber;
            ShowLoader(resourcesLoaded < resourcesTarget ? 1 : 0, 100 * resourcesLoaded / resourcesTarget);
        }

        protected override void OnKeyEvent(Key key) {
            if (key.State != Key.StateType.Down)
                return;
            lastAction = DateTime.Now;
            switch (key.KeyPressedName) {
                case "Right":
                    if (menuShown == 0)
                        break;
                    if (selectedTile < tilesNumber - 1) {
                        selectedTile = (selectedTile + 1) % tilesNumber;
                    }
                    SelectTile(selectedTile);
                    Log.Info("JuvoGL", "->");
                    break;
                case "Left":
                    if (menuShown == 0)
                        break;
                    if (selectedTile > 0) {
                        selectedTile = (selectedTile - 1 + tilesNumber) % tilesNumber;
                    }
                    SelectTile(selectedTile);
                    Log.Info("JuvoGL", "<-");
                    break;
                case "space":
                case "0":
                    menuShown = (menuShown + 1) % 2;
                    ShowMenu(menuShown);
                    Log.Info("JuvoGL", "0");
                    break;
                case "1":
                    SwitchTextRenderingMode();
                    break;
                case "2":
                    SwitchFPSCounterVisibility();
                    break;
                case "Return":
                    if (selectedTile >= contentList.Count)
                        return;
                    /*ClipDefinition clip = new ClipDefinition() {
                        Title = "HLS",
                        Type = "hls",
                        Url = "http://multiplatform-f.akamaihd.net/i/multi/april11/sintel/sintel-hd_,512x288_450_b,640x360_700_b,768x432_1000_b,1024x576_1400_m,.mp4.csmil/master.m3u8",
                        Subtitles = new List<SubtitleInfo>(),
                        Poster = "",
                        Description = "",
                        DRMDatas = new List<DRMDescription>()
                    };*/

                    if(player == null)
                        player = new PlayerService();

                    Log.Info("JuvoGL", "Playing " + contentList[selectedTile].Title + " (" + contentList[selectedTile].Source + ")");
                    player.SetSource(contentList[selectedTile].Clip);

                    if (menuShown == 0)
                        break;
                    menuShown = 0;
                    ShowMenu(menuShown);
                    break;
                case "XF86Back":
                    if (menuShown == 1)
                        break;
                    menuShown = 1;
                    ShowMenu(menuShown);
                    if (player != null) {
                        player.Dispose();
                        player = null;
                    }
                    break;
                case "XF86Exit":
                    Exit();
                    break;
                case "XF86AudioMute": // Mute
                case "XF863XSpeed": // Play
                    Log.Info("JuvoGL", "Play");
                    if (player != null)
                        player.Start();
                    break;
                case "XF86AudioPause": // Pause
                    if (player != null)
                        player.Pause();
                    break;
                case "XF863D": // Stop
                    if (player != null)
                        player.Stop();
                    break;
                case "XF86AudioRewind": // Seek forward
                case "XF86AudioNext": // Seek backwards
                case "XF86Info": // Info
                case "XF86Red": // A
                case "XF86Green": // B
                case "XF86Yellow": // C
                case "XF86Blue": // D
                    break;
                default:
                    Log.Info("JuvoGL", "Unknown key pressed: " + key.KeyPressedName);
                    break;
            }
            progressBarShown = (menuShown + 1) % 2;
        }

        private void UpdateUI() {
            if (player != null && tiles.Length > selectedTile) {
                playerTimeCurrentPosition = (int)player.CurrentPosition.TotalMilliseconds;
                playerTimeDuration = (int)player.Duration.TotalMilliseconds;

                switch (player.State) {
                    case PlayerState.Idle:
                        playerState = 0;
                        break;
                    case PlayerState.Preparing:
                        playerState = 1;
                        break;
                    case PlayerState.Prepared:
                        playerState = 2;
                        if (player != null)
                            player.Start();
                        break;
                    case PlayerState.Stopped:
                        playerState = 3;
                        break;
                    case PlayerState.Paused:
                        playerState = 4;
                        break;
                    case PlayerState.Playing:
                        playerState = 5;
                        break;
                    case PlayerState.Error:
                        playerState = 6;
                        break;
                }
            }
            if (progressBarShown != 0 && playerState == (int)PlayerState.Playing && (DateTime.Now - lastAction).TotalMilliseconds >= prograssBarFadeout.TotalMilliseconds) {
                progressBarShown = 0;
                Log.Info("JuvoGL", "Hiding progress bar (" + (DateTime.Now - lastAction).TotalMilliseconds + " >= " + prograssBarFadeout.TotalMilliseconds + ").");
            }
            fixed (byte* name = GetBytes(/*tiles[selectedTile].name*/ contentList[selectedTile].Title)) {
                UpdatePlaybackControls(progressBarShown, playerState, playerTimeCurrentPosition, playerTimeDuration, name, /*tiles[selectedTile].name.Length*/ contentList[selectedTile].Title.Length);
            }
        }

        protected override void OnUpdate(IntPtr eglDisplay, IntPtr eglSurface)
        {
            LoadResources();
            UpdateUI();
            Draw(eglDisplay, eglSurface);
        }

        private static void Main(string[] args)
        {
            Program myProgram = new Program();
            myProgram.Run(args);
        }
    }
}