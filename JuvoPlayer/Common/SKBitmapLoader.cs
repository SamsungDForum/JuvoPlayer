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

using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.ResourceLoaders;
using SkiaSharp;

namespace JuvoPlayer.Common
{
    public static class SKBitmapLoader
    {
        public static Task<SKBitmap> Load(IResource resource, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                using (var stream = await resource.ReadAsStreamAsync())
                {
                    return token.IsCancellationRequested ? null : SKBitmap.Decode(stream);
                }
            }, token);
        }

        public static async Task<SKBitmap> Load(string resourcePath, CancellationToken token)
        {
            using (var resource = ResourceFactory.Create(resourcePath))
                return await Load(resource, token);
        }
    }
}