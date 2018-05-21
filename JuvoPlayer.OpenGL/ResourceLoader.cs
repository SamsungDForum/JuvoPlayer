using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageSharp;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;

namespace JuvoPlayer.OpenGL
{
    internal unsafe class ResourceLoader
    {
        public int TilesCount { get; private set; } = 0;
        private int _tilesCountTarget = 0;
        private Queue<Tile> _loadedTiles = new Queue<Tile>();

        public int FontsCount { get; private set; } = 0;
        private readonly int _fontsCountTarget = 1;
        private Queue<Font> _loadedFonts = new Queue<Font>();

        public int IconsCount { get; private set; } = 0;
        private readonly int _iconsCountTarget = Icons.Length;
        private Queue<Icon> _loadedIcons = new Queue<Icon>();

        public List<ClipDefinition> ContentList { get; private set; }

        public ILogger Logger { private get; set; }

        private static readonly Icon[] Icons =
        {
            new Icon
            {
                Id = IconType.Play,
                Image = new ImageData { Path = "play.png" }
            },
            new Icon
            {
                Id = IconType.Resume,
                Image = new ImageData { Path = "resume.png" }
            },
            new Icon
            {
                Id = IconType.Stop,
                Image = new ImageData { Path = "stop.png" }
            },
            new Icon
            {
                Id = IconType.Pause,
                Image = new ImageData { Path = "pause.png" }
            },
            new Icon
            {
                Id = IconType.FastForward,
                Image = new ImageData { Path = "fast-forward.png" }
            },
            new Icon
            {
                Id = IconType.Rewind,
                Image = new ImageData { Path = "rewind.png" }
            },
            new Icon
            {
                Id = IconType.SkipToEnd,
                Image = new ImageData { Path = "skip-to-end.png" }
            },
            new Icon
            {
                Id = IconType.SkipToStart,
                Image = new ImageData { Path = "skip-to-start.png" }
            }
        };

        public void LoadResources(string fullExecutablePath, bool loadTestContentList = false)
        {
            var clipsFilePath =
                Path.Combine(fullExecutablePath, "shared", "res",
                    loadTestContentList ? "testvideoclips.json" : "videoclips.json");
            LoadContentList(clipsFilePath);

            var resourcesDirPath = Path.Combine(fullExecutablePath, "res");
            InitLoadingFonts(resourcesDirPath);
            InitLoadingTiles(resourcesDirPath);
            InitLoadingIcons(resourcesDirPath);
        }

        private void LoadContentList(string filePath)
        {
            ContentList = JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(filePath).ToList();
        }

        private void InitLoadingFonts(string dirPath)
        {
            _loadedFonts = new Queue<Font>();
            LoadFont(new Font() { Id = 0, FontPath = Path.Combine(dirPath, "fonts/akashi.ttf")});
        }

        private void InitLoadingTiles(string dirPath)
        {
            _loadedTiles = new Queue<Tile>();
            _tilesCountTarget = ContentList.Count;
            foreach (var contentItem in ContentList)
            {
                var tile = new Tile
                {
                    Image = new ImageData() { Path = Path.Combine(dirPath, "tiles", contentItem.Poster) },
                    Description = contentItem.Description ?? "",
                    Name = contentItem.Title ?? "",
                    Id = DllImports.AddTile()
                };
                LoadTile(tile);
            }
        }

        private void InitLoadingIcons(string dirPath)
        {
            _loadedIcons = new Queue<Icon>();
            for (var i = 0; i < Icons.Length; ++i)
            {
                Icons[i].Image.Path = Path.Combine(dirPath, "icons", Icons[i].Image.Path);
                LoadIcon(Icons[i]);
            }
        }

        private void LoadFont(Font font)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(font.FontPath))
                    {
                        var fontData = new byte[stream.Length];
                        stream.Read(fontData, 0, (int) stream.Length);
                        lock (_loadedFonts)
                        {
                            font.FontData = fontData;
                            _loadedFonts.Enqueue(font);
                        }

                        Logger?.Info("Loaded font: " + font.FontPath + " (" + fontData.Length + "b)");
                    }
                }
                catch (Exception e)
                {
                    Logger?.Info(e
                        .ToString());
                }
            });
        }

        private void LoadTile(Tile tile)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(tile.Image.Path))
                    {
                        var image = new Image(stream);
                        var pixels = GetPixels(image, ColorSpace.RGB);

                        tile.Image.Width = image.Width;
                        tile.Image.Height = image.Height;
                        tile.Image.Pixels = pixels;
                        lock (_loadedTiles)
                        {
                            _loadedTiles.Enqueue(tile);
                        }

                        Logger?.Info("Loaded tile: " + tile.Image.Path + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                    }
                }
                catch (Exception e)
                {
                    Logger?.Info(e
                        .ToString());
                }
            });
        }

        private void LoadIcon(Icon icon)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(icon.Image.Path))
                    {
                        var image = new Image(stream);
                        var pixels = GetPixels(image, ColorSpace.RGBA);

                        icon.Image.Width = image.Width;
                        icon.Image.Height = image.Height;
                        icon.Image.Pixels = pixels;
                        lock (_loadedTiles)
                        {
                            _loadedIcons.Enqueue(icon);
                        }

                        Logger?.Info("Loaded icon: " + icon.Image.Path + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                    }
                }
                catch (Exception e)
                {
                    Logger?.Info(e
                        .ToString());
                }
            });
        }

        private byte[] GetPixels(Image image, ColorSpace colorSpace)
        {
            int channels;
            switch (colorSpace)
            {
                case ColorSpace.RGB:
                    channels = 3;
                    break;
                case ColorSpace.RGBA:
                    channels = 4;
                    break;
                default:
                    Logger.Error("Wrong color space.");
                    return new byte[] {};
            }
            var pixels = new byte[image.Pixels.Length * channels];
            for (var i = 0; i < image.Pixels.Length; ++i)
            {
                pixels[channels * i + 0] = image.Pixels[i].R;
                pixels[channels * i + 1] = image.Pixels[i].G;
                pixels[channels * i + 2] = image.Pixels[i].B;
                if(colorSpace == ColorSpace.RGBA)
                    pixels[channels * i + 3] = image.Pixels[i].A;
            }
            return pixels;
        }

        public static byte[] GetBytes(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

        public void LoadQueuedResources()
        {
            var resourcesTarget = _tilesCountTarget + _fontsCountTarget + _iconsCountTarget;
            var resourcesLoaded = TilesCount + FontsCount + IconsCount;
            if (resourcesLoaded >= resourcesTarget)
                return;
            LoadQueuedTiles();
            LoadQueuedFonts();
            LoadQueuedIcons();
        }

        private void LoadQueuedTiles()
        {
            lock (_loadedTiles)
            {
                while (_loadedTiles.Count > 0)
                {
                    try
                    {
                        var tile = _loadedTiles.Dequeue();
                        fixed (byte* p = tile.Image.Pixels, name = GetBytes(tile.Name), desc = GetBytes(tile.Description))
                        {
                            DllImports.SetTileData(tile.Id, p, tile.Image.Width, tile.Image.Height, name, tile.Name.Length, desc, tile.Description.Length);
                        }

                        ++TilesCount;
                        UpdateLoader();
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e.ToString());
                    }
                }
            }
        }

        private void LoadQueuedFonts()
        {
            lock (_loadedFonts)
            {
                while (_loadedFonts.Count > 0)
                {
                    try
                    {
                        var font = _loadedFonts.Dequeue();
                        fixed (byte* p = font.FontData)
                        {
                            DllImports.AddFont(p, font.FontData.Length);
                        }

                        ++FontsCount;
                        UpdateLoader();
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e.ToString());
                    }
                }
            }
        }

        private void LoadQueuedIcons()
        {
            lock (_loadedIcons)
            {
                while (_loadedIcons.Count > 0)
                {
                    try
                    {
                        var icon = _loadedIcons.Dequeue();
                        fixed (byte* p = icon.Image.Pixels)
                        {
                            DllImports.SetIcon((int)icon.Id, p, icon.Image.Width, icon.Image.Height);
                        }

                        ++IconsCount;
                        UpdateLoader();
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e.ToString());
                    }
                }
            }
        }

        private void UpdateLoader()
        {
            var resourcesTarget = _tilesCountTarget + _fontsCountTarget + _iconsCountTarget;
            var resourcesLoaded = TilesCount + FontsCount + IconsCount;
            DllImports.ShowLoader(resourcesLoaded < resourcesTarget ? 1 : 0,
                resourcesTarget > 0 ? 100 * resourcesLoaded / resourcesTarget : 0);
        }
    }
}