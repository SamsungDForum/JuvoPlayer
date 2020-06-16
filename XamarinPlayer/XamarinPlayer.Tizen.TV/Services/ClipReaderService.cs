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
using System.Linq;
using System.Threading.Tasks;
using Configuration;
using JuvoPlayer.Common;
using JuvoPlayer.ResourceLoaders;
using JuvoPlayer.Utils;
using Xamarin.Forms;
using XamarinPlayer.Tizen.TV.Services;

[assembly: Dependency(typeof(ClipReaderService))]

namespace XamarinPlayer.Tizen.TV.Services
{
    internal class ClipReaderService : IClipReaderService
    {
        public Task<List<Clip>> ReadClips()
        {
            return Task.Run(async () =>
            {
                using (var resource = ResourceFactory.Create(Paths.VideoClipJsonPath))
                {
                    var content = await resource.ReadAsStringAsync();
                    return JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(content).Select(o =>
                    {
                        if (o.SeekPreviewPath != null)
                            o.SeekPreviewPath = resource.Resolve(o.SeekPreviewPath).AbsolutePath;
                        if (o.TilePreviewPath != null)
                            o.TilePreviewPath = resource.Resolve(o.TilePreviewPath).AbsolutePath;
                        o.Poster = resource.Resolve(o.Poster).AbsolutePath;

                        var clip = new Clip
                        {
                            Image = o.Poster, Description = o.Description, Source = o.Url,
                            Title = o.Title,
                            ClipDetailsHandle = o, TilePreviewPath = o.TilePreviewPath
                        };
                        return clip;
                    }).ToList();
                }
            });
        }
    }
}