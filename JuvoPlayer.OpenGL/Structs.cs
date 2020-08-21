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

 namespace JuvoPlayer.OpenGL
{
    public enum PlayerState // int values are passed down to native code
    {
        Idle = 0,
        Prepared = 1,
        Playing = 2,
        Paused = 3
    }

    internal enum Format
    {
        Rgba,
        Bgra,
        Rgb,
        Unknown
    }

    internal struct ImageData
    {
        public string Path;
        public int Width;
        public int Height;
        public byte[] Pixels;
        public Format Format;
    }

    internal enum IconType
    {
        Play,
        Resume,
        Stop,
        Pause,
        FastForward,
        Rewind,
        SkipToEnd,
        SkipToStart,
        Options
    };

    internal struct Icon
    {
        public IconType Id;
        public ImageData Image;
    }

    internal enum ColorSpace
    {
        RGB,
        RGBA
    }
}