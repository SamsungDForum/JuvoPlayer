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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using Xamarin.Forms;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.TV.Services;
using Application = Tizen.Applications.Application;

[assembly: Dependency(typeof(ClipReaderService))]

namespace XamarinPlayer.Tizen.TV.Services
{
    class ClipReaderService : IClipReaderService
    {
        private string ApplicationPath => Path.GetDirectoryName(
            Path.GetDirectoryName(Application.Current.ApplicationInfo.ExecutablePath));

        public List<Clip> ReadClips()
        {
            var clipsPath = Path.Combine(ApplicationPath, "shared", "res", "videoclips.json");

            return JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(clipsPath).Select(
                o => new Clip
                {
                    Image = o.Poster, Description = o.Description, Source = o.Url, Title = o.Title,
                    ClipDetailsHandle = o
                }
            ).ToList();
        }
    }
}