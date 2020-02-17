/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using JuvoPlayer.ResourceLoaders;

namespace JuvoPlayer.Common
{
    public class SKBitmapCache
    {
        private readonly Dictionary<string, WeakReference<SKBitmapRefCounted>> _bitmaps =
            new Dictionary<string, WeakReference<SKBitmapRefCounted>>();

        private readonly ConcurrentDictionary<string, SemaphoreSlim> _bitmapLoaderLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public async Task<SKBitmapRefCounted> GetBitmap(string path)
        {
            if (TryGetBitmap(path, out var bitmap))
            {
                OnCacheHit(path);
                return bitmap;
            }

            var loaderLock = GetLoaderLock(path);

            await loaderLock.WaitAsync();
            try
            {
                if (TryGetBitmap(path, out bitmap))
                {
                    OnCacheHit(path);
                    return bitmap;
                }

                OnCacheMiss(path);
                return await LoadBitmap(path);
            }
            finally
            {
                loaderLock.Release();
            }
        }

        public Task<SKBitmapRefCounted> GetBitmap(IResource resource)
        {
            return GetBitmap(resource.AbsolutePath);
        }

        private bool TryGetBitmap(string path, out SKBitmapRefCounted bitmap)
        {
            bitmap = null;
            if (!_bitmaps.TryGetValue(path, out var reference) || !reference.TryGetTarget(out bitmap))
                return false;
            if (bitmap.IsDisposed)
                return false;
            bitmap.Share();
            return true;
        }

        private SemaphoreSlim GetLoaderLock(string path)
        {
            return _bitmapLoaderLocks.GetOrAdd(path, s => new SemaphoreSlim(1, 1));
        }

        private async Task<SKBitmapRefCounted> LoadBitmap(string path)
        {
            var skBitmap = await SKBitmapLoader.Load(path, CancellationToken.None);
            var skBitmapRefCounted = new SKBitmapRefCounted(skBitmap);
            _bitmaps[path] = new WeakReference<SKBitmapRefCounted>(skBitmapRefCounted);
            skBitmapRefCounted.Share();
            return skBitmapRefCounted;
        }

        private void OnCacheHit(string path)
        {
            _logger.Debug(path);
        }

        private void OnCacheMiss(string path)
        {
            _logger.Debug(path);
        }
    }
}