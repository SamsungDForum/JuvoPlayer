/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using System.Collections.Generic;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.OpenGL
{
    class StoryboardManager
    {
        private static StoryboardManager _instance;

        public static StoryboardManager GetInstance()
        {
            return _instance ?? (_instance = new StoryboardManager());
        }

        protected StoryboardManager()
        {
        }

        private StoryboardReader _tilePreviewReader;
        private int _tilePreviewReaderId = -1;
        private readonly Dictionary<int, string> _tilePreviewPath = new Dictionary<int, string>();

        private SeekLogic _seekLogic;
        private StoryboardReader _seekPreviewReader;

        private readonly DllImports.GetTilePreviewStoryboardDelegate _getTilePreviewStoryboardDelegate = GetStoryboardData; // so it's not GC-ed while used in native code
        private readonly DllImports.GetSeekPreviewStoryboardDelegate _getSeekPreviewStoryboardDelegate = GetSeekPreviewStoryboardDataDelegate; // so it's not GC-ed while used in native code

        public DllImports.GetTilePreviewStoryboardDelegate AddTile(int tileId)
        {
            if (ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath != null)
                _tilePreviewPath.Add(tileId, ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath);

            return _getTilePreviewStoryboardDelegate;
        }

        public void UnloadTilePreview()
        {
            _tilePreviewReaderId = -1;
            _tilePreviewReader?.Dispose();
            _tilePreviewReader = null;
        }

        public static DllImports.StoryboardData GetStoryboardData(long position, int tileId)
        {
            return GetInstance().GetStoryboardDataImpl(position, tileId);
        }

        private DllImports.StoryboardData GetStoryboardDataImpl(long position, int tileId)
        {
            const float tilePreviewTimeScale = 10.0f / 3.0f;

            if (tileId != _tilePreviewReaderId)
            {
                _tilePreviewReaderId = tileId;
                _tilePreviewReader?.Dispose();
                if (_tilePreviewPath.ContainsKey(tileId))
                    _tilePreviewReader = new StoryboardReader(
                        ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath,
                        StoryboardReader.PreloadingStrategy.PreloadOnlyRemoteSources);
                else
                {
                    _tilePreviewReader = null;
                    return GetStoryboardData();
                }
            }

            if (!_tilePreviewReader.LoadTask.IsCompletedSuccessfully)
                return GetStoryboardData(1);

            var subSkBitmap = _tilePreviewReader.GetFrame(TimeSpan.FromMilliseconds(position) * tilePreviewTimeScale);
            if (subSkBitmap == null)
                return GetStoryboardData(1, 1, 0, new DllImports.SubBitmap(),
                    (long) (_tilePreviewReader.Duration().TotalMilliseconds / tilePreviewTimeScale));

            return GetStoryboardData(1, 1, 1, GetFrame(subSkBitmap),
                (long) (_tilePreviewReader.Duration().TotalMilliseconds / tilePreviewTimeScale));
        }

        public void SetSeekPreviewReader(StoryboardReader storyboardReader, SeekLogic seekLogic)
        {
            _seekPreviewReader?.Dispose();
            _seekPreviewReader = storyboardReader;
            seekLogic.StoryboardReader = _seekPreviewReader;
            DllImports.SetSeekPreviewCallback(_getSeekPreviewStoryboardDelegate);
            _seekLogic = seekLogic;
        }

        public SubSkBitmap GetSeekPreviewFrame()
        {
            return _seekLogic?.GetSeekPreviewFrame();
        }

        public bool ShallDisplaySeekPreview()
        {
            return _seekLogic?.ShallDisplaySeekPreview() ?? false;
        }

        public static DllImports.StoryboardData GetSeekPreviewStoryboardDataDelegate()
        {
            if (!GetInstance().ShallDisplaySeekPreview())
                return GetStoryboardData();

            var subSkBitmap = GetInstance().GetSeekPreviewFrame();
            if (subSkBitmap == null)
                return GetStoryboardData(1, 1);

            return GetStoryboardData(1, 1, 1, GetFrame(subSkBitmap));
        }

        private static DllImports.StoryboardData GetStoryboardData(int isStoryboardValid = 0, int isStoryboardReady = 0,
            int isFrameReady = 0, DllImports.SubBitmap frame = new DllImports.SubBitmap(), long duration = 0)
        {
            return new DllImports.StoryboardData
            {
                isStoryboardValid = isStoryboardValid,
                isStoryboardReady = isStoryboardReady,
                isFrameReady = isFrameReady,
                frame = frame,
                duration = duration
            };
        }

        private static unsafe DllImports.SubBitmap GetFrame(SubSkBitmap subSkBitmap)
        {
            return new DllImports.SubBitmap
            {
                rectLeft = subSkBitmap.SkRect.Left,
                rectRight = subSkBitmap.SkRect.Right,
                rectTop = subSkBitmap.SkRect.Top,
                rectBottom = subSkBitmap.SkRect.Bottom,
                bitmapWidth = subSkBitmap.Bitmap.Width,
                bitmapHeight = subSkBitmap.Bitmap.Height,
                bitmapInfoColorType = (int) SkiaUtils.ConvertToFormat(subSkBitmap.Bitmap.Info.ColorType),
                bitmapBytes = (byte*) subSkBitmap.Bitmap.GetPixels(),
                bitmapHash = subSkBitmap.SkRect.GetHashCode() % (int.MaxValue - 1) + 1 // let 0 be invalid hash value
            };
        }
    }
}
