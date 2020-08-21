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

using System.IO;
using System.Threading.Tasks;
using Tizen.Applications;

namespace JuvoPlayer.ResourceLoaders
{
    public class FileResource : IResource
    {
        private Stream _stream;
        private bool _disposed;

        public string AbsolutePath { get; }

        public FileResource(string path)
        {
            AbsolutePath = ToAbsolute(path);
        }

        private static string ToAbsolute(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            var resDirectory = Application.Current.DirectoryInfo.Resource;
            return Path.Combine(resDirectory, path);
        }

        public Task<Stream> ReadAsStreamAsync()
        {
            _stream = OpenRead();
            return Task.FromResult(_stream);
        }

        public Task<string> ReadAsStringAsync()
        {
            _stream = OpenRead();
            return new StreamReader(_stream).ReadToEndAsync();
        }

        private Stream OpenRead()
        {
            if (_stream != null) return _stream;
            _stream = File.OpenRead(AbsolutePath);
            return _stream;
        }

        public IResource Resolve(string path)
        {
            var parentDirectory = Path.GetDirectoryName(AbsolutePath);
            return new FileResource(Path.Combine(parentDirectory, path));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stream?.Dispose();
        }
    }
}