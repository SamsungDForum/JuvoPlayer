using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageSharp;
using JuvoPlayer.OpenGL.Services;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private int _tilesNumber = 0;
        private int _tilesNumberTarget = 0;
        private Queue<Tile> _loadedTiles;

        private int _fontsNumber = 0;
        private readonly int _fontsNumberTarget = 1;
        private Queue<KeyValuePair<List<int>, byte[]>> _loadedFonts;

        private int _iconsNumber = 0;
        private readonly int _iconsNumberTarget = Icons.Length;
        private Queue<Icon> _loadedIcons;

        private List<DetailContentData> ContentList { get; set; }
        private List<Clip> Clips { get; set; }

        private static readonly Icon[] Icons =
        {
            new Icon
            {
                Id = IconType.Play,
                ImgPath = "play.png"
            },
            new Icon
            {
                Id = IconType.Resume,
                ImgPath = "resume.png"
            },
            new Icon
            {
                Id = IconType.Stop,
                ImgPath = "stop.png"
            },
            new Icon
            {
                Id = IconType.Pause,
                ImgPath = "pause.png"
            },
            new Icon
            {
                Id = IconType.FastForward,
                ImgPath = "fast-forward.png"
            },
            new Icon
            {
                Id = IconType.Rewind,
                ImgPath = "rewind.png"
            },
            new Icon
            {
                Id = IconType.SkipToEnd,
                ImgPath = "skip-to-end.png"
            },
            new Icon
            {
                Id = IconType.SkipToStart,
                ImgPath = "skip-to-start.png"
            }
        };

        private void LoadResources()
        {
            var clipsFilePath =
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)),
                    "shared", "res", LoadTestContentList ? "testvideoclips.json" : "videoclips.json");
            LoadContentList(clipsFilePath);

            var resourcesDirPath = Path.Combine(
                Path.GetDirectoryName(Path.GetDirectoryName(Current.ApplicationInfo.ExecutablePath)), "res/");
            InitLoadingFonts(resourcesDirPath);
            InitLoadingTiles(resourcesDirPath);
            InitLoadingIcons(resourcesDirPath);
        }

        private void LoadContentList(string filePath)
        {
            Clips = ClipReaderService.ReadClips(filePath);
            ContentList = Clips.Select(o => new DetailContentData()
            {
                Bg = o.Image,
                Clip = o.ClipDetailsHandle,
                ContentFocusedCommand = null,
                Description = o.Description,
                Image = o.Image,
                Source = o.Source,
                Title = o.Title,
            }).ToList();
        }

        private void InitLoadingFonts(string dirPath)
        {
            _loadedFonts = new Queue<KeyValuePair<List<int>, byte[]>>();
            LoadFont(dirPath + "fonts/akashi.ttf");
        }

        private void InitLoadingTiles(string dirPath)
        {
            _loadedTiles = new Queue<Tile>();
            _tilesNumberTarget = ContentList.Count;
            foreach (var contentItem in ContentList)
            {
                var tile = new Tile
                {
                    ImgPath = dirPath + "tiles/" + contentItem.Image,
                    Description = contentItem.Description ?? "",
                    Name = contentItem.Title ?? ""
                };
                LoadTile(tile);
            }
        }

        private void InitLoadingIcons(string dirPath)
        {
            _loadedIcons = new Queue<Icon>();
            for (var i = 0; i < Icons.Length; ++i)
            {
                Icons[i].ImgPath = dirPath + "icons/" + Icons[i].ImgPath;
                LoadIcon(Icons[i]);
            }
        }

        private void LoadFont(string file)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var font = new byte[stream.Length];
                        stream.Read(font, 0, (int)stream.Length);
                        lock (_loadedFonts)
                        {
                            _loadedFonts.Enqueue(
                                new KeyValuePair<List<int>, byte[]>(new List<int>(new int[] { }), font));
                        }
                        _logger?.Info("Loaded font: " + file + " (" + font.Length + "b)");
                    }
                }
                catch (Exception e)
                {
                    _logger?.Info(e.ToString()); // TODO(g.skowinski): With current logic the loading will never end if one element doesn't load. Handle it?
                }
            });
        }

        private void LoadTile(Tile tile)
        {
            tile.Id = AddTile();
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(tile.ImgPath))
                    {
                        var image = new Image(stream);
                        var pixels = new byte[image.Pixels.Length * 3];
                        for (var i = 0; i < image.Pixels.Length; ++i)
                        {
                            pixels[3 * i + 0] = image.Pixels[i].R;
                            pixels[3 * i + 1] = image.Pixels[i].G;
                            pixels[3 * i + 2] = image.Pixels[i].B;
                        }
                        tile.ImgWidth = image.Width;
                        tile.ImgHeight = image.Height;
                        tile.ImgPixels = pixels;
                        lock (_loadedTiles)
                        {
                            _loadedTiles.Enqueue(tile);
                        }
                        _logger?.Info("Loaded tile: " + tile.ImgPath + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                    }
                }
                catch (Exception e)
                {
                    _logger?.Info(e.ToString()); // TODO(g.skowinski): With current logic the loading will never end if one element doesn't load. Handle it?
                }
            });
        }

        private void LoadIcon(Icon icon)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(icon.ImgPath))
                    {
                        var image = new Image(stream);
                        var pixels = new byte[image.Pixels.Length * 4];
                        for (var i = 0; i < image.Pixels.Length; ++i)
                        {
                            pixels[4 * i + 0] = image.Pixels[i].R;
                            pixels[4 * i + 1] = image.Pixels[i].G;
                            pixels[4 * i + 2] = image.Pixels[i].B;
                            pixels[4 * i + 3] = image.Pixels[i].A;
                        }
                        icon.ImgWidth = image.Width;
                        icon.ImgHeight = image.Height;
                        icon.ImgPixels = pixels;
                        lock (_loadedTiles)
                        {
                            _loadedIcons.Enqueue(icon);
                        }
                        _logger?.Info("Loaded icon: " + icon.ImgPath + ": " + image.Width + "x" + image.Height + " = " + image.Pixels.Length + (image.IsAnimated ? " (" + image.Frames.Count + " frames)" : ""));
                    }
                }
                catch (Exception e)
                {
                    _logger?.Info(e.ToString()); // TODO(g.skowinski): With current logic the loading will never end if one element doesn't load. Handle it?
                }
            });
        }

        private static byte[] GetBytes(string str)
        {
            var a = new byte[str.Length];
            for (var i = 0; i < str.Length; ++i)
                a[i] = (byte) str[i];
            return a;
        }

        private void LoadQueuedResources()
        {
            var resourcesTarget = _tilesNumberTarget + _fontsNumberTarget + _iconsNumberTarget;
            var resourcesLoaded = _tilesNumber + _fontsNumber + _iconsNumber;
            if (resourcesLoaded >= resourcesTarget)
                return;
            lock (_loadedTiles)
            {
                while (_loadedTiles.Count > 0)
                {
                    try
                    {
                        var tile = _loadedTiles.Dequeue();
                        fixed (byte* p = tile.ImgPixels)
                        {
                            fixed (byte* name = GetBytes(tile.Name))
                            {
                                fixed (byte* desc = GetBytes(tile.Description))
                                {
                                    SetTileData(tile.Id, p, tile.ImgWidth, tile.ImgHeight, name, tile.Name.Length, desc,
                                        tile.Description.Length);
                                }
                            }
                        }

                        ++_tilesNumber;
                        UpdateLoader();
                    }
                    catch (Exception e)
                    {
                        _logger?.Info(e.ToString()); // TODO(g.skowinski): With current logic the loading will never end if one element doesn't load. Handle it?
                    }
                }
            }

            lock (_loadedFonts)
            {
                while (_loadedFonts.Count > 0)
                {
                    try
                    {
                        var font = _loadedFonts.Dequeue();
                        fixed (byte* p = font.Value)
                        {
                            AddFont(p, font.Value.Length);
                        }

                        ++_fontsNumber;
                        UpdateLoader();
                    }
                    catch (Exception e)
                    {
                        _logger?.Info(e.ToString()); // TODO(g.skowinski): With current logic the loading will never end if one element doesn't load. Handle it?
                    }
                }
            }

            lock (_loadedIcons)
            {
                while (_loadedIcons.Count > 0)
                {
                    try
                    {
                        var icon = _loadedIcons.Dequeue();
                        fixed (byte* p = icon.ImgPixels)
                        {
                            SetIcon((int) icon.Id, p, icon.ImgWidth, icon.ImgHeight);
                        }

                        ++_iconsNumber;
                        UpdateLoader();
                    }
                    catch (Exception e)
                    {
                        _logger?.Info(e.ToString()); // TODO(g.skowinski): With current logic the loading will never end if one element doesn't load. Handle it?
                    }
                }
            }
        }

        private void UpdateLoader()
        {
            var resourcesTarget = _tilesNumberTarget + _fontsNumberTarget + _iconsNumberTarget;
            var resourcesLoaded = _tilesNumber + _fontsNumber + _iconsNumber;
            ShowLoader(resourcesLoaded < resourcesTarget ? 1 : 0,
                resourcesTarget > 0 ? 100 * resourcesLoaded / resourcesTarget : 0);
        }
    }
}