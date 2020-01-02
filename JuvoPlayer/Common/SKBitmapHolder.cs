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
using System.Threading.Tasks;
using JuvoPlayer.ResourceLoaders;
using SkiaSharp;

namespace JuvoPlayer.Common
{
    public class SKBitmapHolder : IDisposable
    {
        private SKBitmap _bitmap;
        private bool _isDecoding;
        private bool _isFaulted;
        private readonly IResource _resource;
        private bool IsDisposed { get; set; }

        public SKBitmapHolder(IResource resource)
        {
            _resource = resource;
        }

        public SKBitmap GetBitmap()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(SKBitmapHolder));
            if (_bitmap != null) return _bitmap;
            if (_isDecoding || _isFaulted) return null;

            ScheduleBitmapDecode();
            _isDecoding = true;
            return null;
        }

        private void ScheduleBitmapDecode()
        {
            Task.Run(DecodeBitmap)
                .ContinueWith(OnBitmapDecoded, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private async Task<SKBitmap> DecodeBitmap()
        {
            using (var stream = await _resource.ReadAsStreamAsync())
                return SKBitmap.Decode(stream);
        }

        private void OnBitmapDecoded(Task<SKBitmap> bitmapTask)
        {
            _isDecoding = false;

            if (bitmapTask.Status != TaskStatus.RanToCompletion)
            {
                if (IsDisposed)
                    _resource.Dispose();
                _isFaulted = true;
                return;
            }

            if (IsDisposed)
            {
                _resource.Dispose();
                bitmapTask.Result.Dispose();
            }
            else
                _bitmap = bitmapTask.Result;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            if (!_isDecoding)
                _resource.Dispose();
            _bitmap?.Dispose();
            IsDisposed = true;
        }
    }
}