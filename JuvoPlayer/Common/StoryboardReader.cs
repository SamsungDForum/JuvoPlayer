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
using SkiaSharp;

namespace JuvoPlayer.Common
{
    public class StoryboardReader : IDisposable
    {
        private StoryboardsMap _map;
        private readonly IDictionary<string, SKBitmapHolder> _bitmaps;
        private readonly IStoryboardSource _source;
        private readonly PreloadingStrategy _preloadingStrategy;

        public SKSize FrameSize => new SKSize(_map?.FrameWidth ?? 0, _map?.FrameHeight ?? 0);

        public Task LoadTask { get; }

        public enum PreloadingStrategy
        {
            DoNotPreload,
            PreloadOnlyRemoteSources,
            PreloadEverything,
        }

        public StoryboardReader(string jsonDescPath, PreloadingStrategy preloadingStrategy = PreloadingStrategy.DoNotPreload)
        {
            _source = CreateStoryboardSource(jsonDescPath);
            _preloadingStrategy = preloadingStrategy;
            _bitmaps = new Dictionary<string, SKBitmapHolder>();
            LoadTask = LoadMap();
        }

        private static IStoryboardSource CreateStoryboardSource(string path)
        {
            return IsRemotePath(path)
                ? (IStoryboardSource) new RemoteStoryboardSource(path)
                : new LocalStoryboardSource(path);
        }

        private static bool IsRemotePath(string path)
        {
            return path.StartsWith("http");
        }

        private async Task LoadMap()
        {
            _map = await _source.GetStoryboardsMap();

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
            return _source.GetType() == typeof(RemoteStoryboardSource);
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
            return GetOrCreateBitmapHolder(storyboard).GetBitmap();
        }

        private SKBitmapHolder GetOrCreateBitmapHolder(Storyboard storyboard)
        {
            var key = storyboard.Filename;
            if (_bitmaps.ContainsKey(key)) return _bitmaps[key];

            var bitmapHolder = new SKBitmapHolder(_source.GetBitmap(storyboard));
            _bitmaps[key] = bitmapHolder;
            return bitmapHolder;
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
            foreach (var bitmapHolder in _bitmaps.Values)
                bitmapHolder.Dispose();
            _bitmaps.Clear();
        }
    }
}