/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using SkiaSharp;

namespace JuvoPlayer.OpenGL
{
    internal class SkiaUtils
    {     
        internal static Format ConvertToFormat(SKColorType type)
        {
            switch (type)
            {
                case SKColorType.Unknown:
                case SKColorType.Alpha8:
                case SKColorType.Rgb565:
                case SKColorType.Argb4444:
                case SKColorType.Gray8:
                case SKColorType.RgbaF16:
#if BUILT_FOR_tizen50
                case SKColorType.Index8:
#endif
                    return Format.Unknown;
                case SKColorType.Rgba8888:
                    return Format.Rgba;
                case SKColorType.Bgra8888:
                    return Format.Bgra;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        internal static SKColorType ConvertToSkColorType(Format format)
        {
            switch (format)
            {
                case Format.Rgba:
                    return SKColorType.Rgba8888;
                case Format.Bgra:
                    return SKColorType.Bgra8888;
                case Format.Rgb:
                case Format.Unknown:
                    return SKColorType.Unknown;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        public static bool IsColorTypeSupported(SKColorType type)
        {
            switch (type)
            {
                case SKColorType.Unknown:
                case SKColorType.Alpha8:
                case SKColorType.RgbaF16:
#if BUILT_FOR_tizen50
                case SKColorType.Index8:
#endif
                case SKColorType.Rgb565:
                case SKColorType.Gray8:
                case SKColorType.Argb4444:
                    return false;
                case SKColorType.Rgba8888:
                case SKColorType.Bgra8888:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static SKColorType GetPlatformColorType()
        {
            return IsColorTypeSupported(SKImageInfo.PlatformColorType)
                ? SKImageInfo.PlatformColorType
                : SKColorType.Bgra8888;
        }
    }
}