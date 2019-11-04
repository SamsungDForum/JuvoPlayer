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
using System.IO;
using System.Linq;
using JuvoPlayer.Utils;
using SkiaSharp;

namespace JuvoPlayer.Common
{
    public class StoryboardReader : IDisposable
    {
        private readonly AllStoryboards _data;
        private readonly IDictionary<string, SKBitmapHolder> _bitmaps;
        private readonly string _jsonDescPath;

        public SKSize FrameSize => new SKSize(_data.FrameWidth, _data.FrameHeight);

        public StoryboardReader(string jsonDescPath)
        {
            _jsonDescPath = jsonDescPath;
            _data = JSONFileReader.DeserializeJsonFile<AllStoryboards>(jsonDescPath);
            _bitmaps = new Dictionary<string, SKBitmapHolder>();
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

        private Storyboard FindStoryboard(TimeSpan position)
        {
            return _data.Storyboards.FirstOrDefault(st => HasFrameForPosition(st, position));
        }

        private bool HasFrameForPosition(Storyboard st, TimeSpan position)
        {
            var (begin, end) = GetStoryboardDuration(st);
            return begin <= position && position < end;
        }

        private (TimeSpan, TimeSpan) GetStoryboardDuration(Storyboard storyboard)
        {
            var begin = storyboard.Begin;
            var end = begin + storyboard.FramesCount * _data.FrameDuration;
            return (TimeSpan.FromSeconds(begin), TimeSpan.FromSeconds(end));
        }

        private SKBitmap GetOrLoadBitmap(Storyboard storyboard)
        {
            var key = storyboard.Filename;
            if (!_bitmaps.ContainsKey(key))
            {
                _bitmaps[key] = new SKBitmapHolder
                {
                    Path = ResolveStoryboardPath(storyboard)
                };
            }
            return _bitmaps[key].GetBitmap();
        }

        private string ResolveStoryboardPath(Storyboard storyboard)
        {
            var dir = Path.GetDirectoryName(_jsonDescPath) ?? throw new InvalidOperationException();
            return Path.Combine(dir, storyboard.Filename);
        }

        private SKRect CalculateFramePosition(Storyboard storyboard, TimeSpan position)
        {
            var begin = storyboard.Begin;
            var positionInStoryboard = position.TotalSeconds - begin;
            var frameDuration = _data.FrameDuration;

            var idx = (int) (positionInStoryboard / frameDuration);

            var left = idx % _data.Columns * _data.FrameWidth;
            var top = idx / _data.Columns * _data.FrameHeight;

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