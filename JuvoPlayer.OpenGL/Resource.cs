/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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

using System.IO;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.ResourceLoaders;
using SkiaSharp;
using static JuvoPlayer.OpenGL.SkiaUtils;

namespace JuvoPlayer.OpenGL
{
    abstract class Resource
    {
        public abstract Task Load();

        public abstract void Push(); // must be run from the main thread (thread with main OpenGL context)

        public static byte[] GetBytes(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

        public static async Task<ImageData> GetImage(string path)
        {
            using (var resource = ResourceFactory.Create(path))
            using (var stream = await resource.ReadAsStreamAsync())
            using (var bitmap = SKBitmap.Decode(stream))
            {
                ConvertBitmapIfNecessary(bitmap);

                return new ImageData
                {
                    Path = path,
                    Width = bitmap.Width,
                    Height = bitmap.Height,
                    Pixels = bitmap.Bytes,
                    Format = ConvertToFormat(bitmap.ColorType)
                };
            }
        }

        private static void ConvertBitmapIfNecessary(SKBitmap bitmap)
        {
            if (IsColorTypeSupported(bitmap.ColorType))
                return;
            bitmap.CopyTo(bitmap, GetPlatformColorType());
        }

        protected byte[] GetData(string path)
        {
            byte[] data;
            using (var stream = File.OpenRead(path))
            {
                data = new byte[stream.Length];
                stream.Read(data, 0, (int) stream.Length);
            }

            return data;
        }
    }
}