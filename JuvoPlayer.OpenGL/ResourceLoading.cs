using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ImageSharp;
using Tizen;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
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
                        lock (_loadedFonts) {
                            _loadedFonts.Enqueue(new KeyValuePair<List<int>, byte[]>(new List<int>(new int[] { }), font));
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
            lock (_loadedFonts) {
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
    }
}