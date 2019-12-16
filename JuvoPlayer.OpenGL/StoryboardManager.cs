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
using System.IO;
using JuvoPlayer.Common;
using Tizen.Applications;

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

        private Dictionary<int, StoryboardReader> storyboardReaders = new Dictionary<int, StoryboardReader>();
        private DllImports.GetStoryboardDataDelegate getStoryboardDataDelegate = GetStoryboardData; // so it's not GC-ed while used in native code

        public DllImports.GetStoryboardDataDelegate AddTile(int tileId)
        {
            if (ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath != null)
                storyboardReaders.Add(tileId, new StoryboardReader(Path.Combine(Application.Current.DirectoryInfo.Resource, ResourceLoader.GetInstance().ContentList[tileId].TilePreviewPath)));
            return getStoryboardDataDelegate;
        }

        public static unsafe DllImports.StoryboardData GetStoryboardData(long position, int tileId)
        {
            float TilePreviewTimeScale = 10.0f / 3.0f;
            
            if (!GetInstance().storyboardReaders.ContainsKey(tileId))
                return new DllImports.StoryboardData
                {
                    isStoryboardReaderReady = 0,
                    isFrameReady = 0,
                    duration = 0
                };

            SubSkBitmap subSkBitmap = GetInstance().storyboardReaders[tileId].GetFrame(TimeSpan.FromMilliseconds(position) * TilePreviewTimeScale);
            if(subSkBitmap == null)
                return new DllImports.StoryboardData
                {
                    isStoryboardReaderReady = 1,
                    isFrameReady = 0,
                    duration = (long) (GetInstance().storyboardReaders[tileId].Duration().TotalMilliseconds / TilePreviewTimeScale)
                };

            return new DllImports.StoryboardData {
                isStoryboardReaderReady = 1,
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
                duration = (long)(GetInstance().storyboardReaders[tileId].Duration().TotalMilliseconds / TilePreviewTimeScale)
            };
        }
    }
}
