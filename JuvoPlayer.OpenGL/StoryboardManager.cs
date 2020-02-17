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
        private static StoryboardManager Instance;

        public static StoryboardManager GetInstance()
        {
            return Instance ?? (Instance = new StoryboardManager());
        }

        protected StoryboardManager()
        {
        }

        public void Dispose()
        {
            tilePreviewReader?.Dispose();
            seekPreviewReader?.Dispose();
        }

        private StoryboardReader tilePreviewReader;
        private int tilePreviewReaderId = -1;
        private readonly Dictionary<int, string> tilePreviewPath = new Dictionary<int, string>();

        private SeekLogic seekLogic;
        private StoryboardReader seekPreviewReader;

        private readonly DllImports.GetStoryboardDataDelegate getStoryboardDataDelegate = GetStoryboardData; // so it's not GC-ed while used in native code
        private readonly DllImports.GetSeekPreviewStoryboardDataDelegate getSeekPreviewStoryboardDataDelegate = GetSeekPreviewStoryboardDataDelegate; // so it's not GC-ed while used in native code

        public DllImports.GetStoryboardDataDelegate AddTile(int tileId)
        {
            if (ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath != null)
                tilePreviewPath.Add(tileId, ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath);

            return getStoryboardDataDelegate;
        }

        public void UnloadTilePreview()
        {
            tilePreviewReaderId = -1;
            tilePreviewReader?.Dispose();
            tilePreviewReader = null;
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public static unsafe DllImports.StoryboardData GetStoryboardData(long position, int tileId)
        {
            const float TilePreviewTimeScale = 10.0f / 3.0f;

            if (tileId != GetInstance().tilePreviewReaderId)
            {
                GetInstance().tilePreviewReaderId = tileId;
                GetInstance().tilePreviewReader?.Dispose();
                if (GetInstance().tilePreviewPath.ContainsKey(tileId))
                    GetInstance().tilePreviewReader = new StoryboardReader(
                        ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath,
                        StoryboardReader.PreloadingStrategy.PreloadOnlyRemoteSources);
                else
                    GetInstance().tilePreviewReader = null;
            }

            if (!GetInstance().tilePreviewPath.ContainsKey(tileId))
                return new DllImports.StoryboardData
                {
                    isStoryboardValid = 0
                };

            if(!GetInstance().tilePreviewReader.LoadTask.IsCompletedSuccessfully)
                return new DllImports.StoryboardData
                {
                    isStoryboardValid = 1,
                    isStoryboardReady = 0
                };

            var subSkBitmap = GetInstance().tilePreviewReader.GetFrame(TimeSpan.FromMilliseconds(position) * TilePreviewTimeScale);
            if (subSkBitmap == null)
                return new DllImports.StoryboardData
                {
                    isStoryboardValid = 1,
                    isStoryboardReady = 1,
                    isFrameReady = 0,
                    duration = (long) (GetInstance().tilePreviewReader.Duration().TotalMilliseconds / TilePreviewTimeScale)
                };

            return new DllImports.StoryboardData
            {
                isStoryboardValid = 1,
                isStoryboardReady = 1,
                isFrameReady = 1,
                frame = new DllImports.SubBitmap
                {
                    rectLeft = subSkBitmap.SkRect.Left,
                    rectRight = subSkBitmap.SkRect.Right,
                    rectTop = subSkBitmap.SkRect.Top,
                    rectBottom = subSkBitmap.SkRect.Bottom,
                    bitmapWidth = subSkBitmap.Bitmap.Width,
                    bitmapHeight = subSkBitmap.Bitmap.Height,
                    bitmapInfoColorType = (int)SkiaUtils.ConvertToFormat(subSkBitmap.Bitmap.Info.ColorType),
                    bitmapBytes = (byte*)subSkBitmap.Bitmap.GetPixels(),
                    bitmapHash = subSkBitmap.SkRect.GetHashCode()
                },
                duration = (long)(GetInstance().tilePreviewReader.Duration().TotalMilliseconds / TilePreviewTimeScale)
            };
        }

        public void SetSeekPreviewReader(StoryboardReader storyboardReader, SeekLogic seekLogic)
        {
            seekPreviewReader?.Dispose();
            seekPreviewReader = storyboardReader;
            seekLogic.StoryboardReader = seekPreviewReader;
            DllImports.SetSeekPreviewCallback(getSeekPreviewStoryboardDataDelegate);
            this.seekLogic = seekLogic;
        }

        public SubSkBitmap GetSeekPreviewFrame()
        {
            return seekLogic?.GetSeekPreviewFrame();
        }

        public bool ShallDisplaySeekPreview()
        {
            return seekLogic?.ShallDisplaySeekPreview() ?? false;
        }

        public static unsafe DllImports.StoryboardData GetSeekPreviewStoryboardDataDelegate()
        {
            if (!GetInstance().ShallDisplaySeekPreview())
                return new DllImports.StoryboardData
                {
                    isStoryboardValid = 0,
                    isStoryboardReady = 0,
                    isFrameReady = 0
                };

            var subSkBitmap = GetInstance().GetSeekPreviewFrame();
            if (subSkBitmap == null)
                return new DllImports.StoryboardData
                {
                    isStoryboardValid = 1,
                    isStoryboardReady = 1,
                    isFrameReady = 0
                };

            return new DllImports.StoryboardData
            {
                isStoryboardValid = 1,
                isStoryboardReady = 1,
                isFrameReady = 1,
                frame = new DllImports.SubBitmap
                {
                    rectLeft = subSkBitmap.SkRect.Left,
                    rectRight = subSkBitmap.SkRect.Right,
                    rectTop = subSkBitmap.SkRect.Top,
                    rectBottom = subSkBitmap.SkRect.Bottom,
                    bitmapWidth = subSkBitmap.Bitmap.Width,
                    bitmapHeight = subSkBitmap.Bitmap.Height,
                    bitmapInfoColorType = (int)SkiaUtils.ConvertToFormat(subSkBitmap.Bitmap.Info.ColorType),
                    bitmapBytes = (byte*)subSkBitmap.Bitmap.GetPixels(),
                    bitmapHash = subSkBitmap.SkRect.GetHashCode()
                }
            };
        }
    }
}
