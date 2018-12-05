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

﻿namespace JuvoPlayer.OpenGL
{
    class TileResource : Resource
    {
        private int _id;
        private ImageData _image;
        private string _name;
        private string _description;

        public TileResource(int id, string path, string name, string description) : base()
        {
            _id = id;
            _image.Path = path;
            _name = name;
            _description = description;
        }

        public override void Load()
        {
            _image = GetImage(_image.Path, ColorSpace.RGB);
        }

        public override unsafe void Push()
        {
            fixed (byte* p = _image.Pixels, name = GetBytes(_name), desc = GetBytes(_description))
            {
                DllImports.SetTileData(_id, p, _image.Width, _image.Height, name, _name.Length, desc, _description.Length);
            }
        }
    }
}
