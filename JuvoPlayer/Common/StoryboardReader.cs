/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2019, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using JuvoPlayer.ResourceLoaders;
using JuvoPlayer.Utils;
using SkiaSharp;

namespace JuvoPlayer.Common
{
    public class StoryboardReader : IDisposable
    {
        private StoryboardsMap _map;
        private readonly IDictionary<string, SKBitmapRefCounted> _bitmaps;
        private readonly IResource _mapResource;
        private readonly PreloadingStrategy _preloadingStrategy;
        private readonly SKBitmapCache _bitmapCache;

        public SKSize FrameSize => new SKSize(_map?.FrameWidth ?? 0, _map?.FrameHeight ?? 0);

        public Task LoadTask { get; }

        public bool IsDisposed { get; private set; }

        public enum PreloadingStrategy
        {
            DoNotPreload,
            PreloadOnlyRemoteSources,
            PreloadEverything,
        }

        public StoryboardReader(string jsonDescPath, PreloadingStrategy preloadingStrategy = PreloadingStrategy.DoNotPreload,  SKBitmapCache cache = null)
        {
            _mapResource = ResourceFactory.Create(jsonDescPath);
            _preloadingStrategy = preloadingStrategy;
            _bitmapCache = cache ?? new SKBitmapCache();
            _bitmaps = new Dictionary<string, SKBitmapRefCounted>();
            LoadTask = LoadMap();
        }

        private async Task LoadMap()
        {
            var text = await _mapResource.ReadAsStringAsync();
            _map = JSONFileReader.DeserializeJsonText<StoryboardsMap>(text);

            if (!ShallPreloadBitmaps()) return;
            foreach (var storyboard in _map.Storyboards)
                GetOrLoadBitmap(storyboard);
        }

        private bool ShallPreloadBitmaps()
        {
            switch (_preloadingStrategy)
            {
                case PreloadingStrategy.DoNotPreload:
                    return false;
                case PreloadingStrategy.PreloadOnlyRemoteSources:
                    return HasRemoteSource();
                case PreloadingStrategy.PreloadEverything:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool HasRemoteSource()
        {
            return _mapResource.GetType() == typeof(HttpResource);
        }

        public SubSkBitmap GetFrame(TimeSpan position)
        {
            var storyboard = FindStoryboard(position);
            if (storyboard == null) return null;
            var bitmap = GetOrLoadBitmap(storyboard);
            if (bitmap == null) return null;
            var rect = CalculateFramePosition(storyboard, position);
            return new SubSkBitmap
            {
                Bitmap = bitmap,
                SkRect = rect
            };
        }

        public TimeSpan Duration()
        {
            var last = _map?.Storyboards.Last();
            return last == null ? TimeSpan.Zero : GetStoryboardDuration(last).Item2;
        }

        private Storyboard FindStoryboard(TimeSpan position)
        {
            return _map?.Storyboards.FirstOrDefault(st => HasFrameForPosition(st, position));
        }

        private bool HasFrameForPosition(Storyboard st, TimeSpan position)
        {
            var (begin, end) = GetStoryboardDuration(st);
            return begin <= position && position < end;
        }

        private (TimeSpan, TimeSpan) GetStoryboardDuration(Storyboard storyboard)
        {
            var begin = storyboard.Begin;
            var end = begin + storyboard.FramesCount * _map.FrameDuration;
            return (TimeSpan.FromSeconds(begin), TimeSpan.FromSeconds(end));
        }

        private SKBitmap GetOrLoadBitmap(Storyboard storyboard)
        {
            var key = storyboard.Filename;
            if (_bitmaps.TryGetValue(key, out var bitmap))
                return bitmap.Value;
            LoadBitmap(storyboard);
            return null;
        }

        private async void LoadBitmap(Storyboard storyboard)
        {
            using (var resource = _mapResource.Resolve(storyboard.Filename))
            {
                var key = storyboard.Filename;
                var bitmap = await _bitmapCache.GetBitmap(resource);
                if (_bitmaps.TryGetValue(key, out _))
                {
                    bitmap.Release();
                    return;
                }
                if (!_bitmaps.ContainsKey(key))
                    _bitmaps.Add(key, bitmap);
            }
        }

        private SKRect CalculateFramePosition(Storyboard storyboard, TimeSpan position)
        {
            Debug.Assert(_map != null, "Map shall be loaded");

            var begin = storyboard.Begin;
            var positionInStoryboard = position.TotalSeconds - begin;
            var frameDuration = _map.FrameDuration;

            var idx = (int) (positionInStoryboard / frameDuration);

            var left = idx % _map.Columns * _map.FrameWidth;
            var top = idx / _map.Columns * _map.FrameHeight;

            return new SKRect
            {
                Left = left,
                Top = top,
                Size = FrameSize
            };
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            var bitmaps = _bitmaps;

            Task.Run(() =>
            {
                foreach (var bitmap in _bitmaps)
                    bitmap.Value.Release();
                bitmaps.Clear();
            });
            _mapResource.Dispose();
        }
    }
}