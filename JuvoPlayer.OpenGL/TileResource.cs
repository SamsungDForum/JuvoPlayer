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

using System;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.ResourceLoaders;

namespace JuvoPlayer.OpenGL
{
    class TileResource : Resource
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly int _id;
        private ImageData _image;
        private readonly string _name;
        private readonly string _description;

        private const string DefaultImage = "tiles/default_bg.png";

        public TileResource(int id, string path, string name, string description)
        {
            _id = id;
            _image.Path = path;
            _name = name;
            _description = description;
        }

        public override async Task Load()
        {
            try
            {
                _image = await GetImage(_image.Path);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);

                if (!_image.Path.EndsWith(DefaultImage))
                {
                    _image.Path = ResourceFactory.Create(DefaultImage).AbsolutePath;
                    await Load();
                }
                else
                    throw;
            }
        }

        public override unsafe void Push()
        {
            fixed (byte* p = _image.Pixels, name = GetBytes(_name), desc = GetBytes(_description))
            {
                DllImports.SetTileData(new DllImports.TileData
                {
                    tileId = _id,
                    pixels = p,
                    width = _image.Width,
                    height = _image.Height,
                    name = name,
                    nameLen = _name.Length,
                    desc = desc,
                    descLen = _description.Length,
                    format = (int) _image.Format,
                    GetTilePreviewStoryboard = StoryboardManager.GetInstance().AddTile(_id)
                });
            }
        }
    }
}