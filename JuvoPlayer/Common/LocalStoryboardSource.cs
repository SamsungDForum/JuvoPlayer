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

using System.IO;
using System.Threading.Tasks;
using JuvoPlayer.Utils;
using Tizen.Applications;

namespace JuvoPlayer.Common
{
    class LocalStoryboardSource : IStoryboardSource
    {
        private readonly string _localPath;

        public LocalStoryboardSource(string localPath)
        {
            _localPath = ToAbsoluteResourcePath(localPath);
        }

        private string ToAbsoluteResourcePath(string path)
        {
            var resourcePath = Application.Current.DirectoryInfo.Resource;
            var finalPath = path;
            if (!path.StartsWith(resourcePath))
                finalPath = Path.Combine(resourcePath, path);
            return finalPath;
        }

        public Task<StoryboardsMap> GetStoryboardsMap()
        {
            return Task.Run(() => JSONFileReader.DeserializeJsonFile<StoryboardsMap>(_localPath));
        }

        public Task<Stream> GetBitmap(Storyboard storyboard)
        {
            return Task.FromResult((Stream) File.OpenRead(ResolvePath(storyboard)));
        }

        private string ResolvePath(Storyboard storyboard)
        {
            var dir = Path.GetDirectoryName(_localPath);
            return Path.Combine(dir, storyboard.Filename);
        }
    }
}