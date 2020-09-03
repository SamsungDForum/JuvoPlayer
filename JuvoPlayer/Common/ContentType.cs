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

using System;

namespace JuvoPlayer.Common
{
    public static class Extensions
    {
        public static ContentType ToContentType(this StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Unknown:
                    return ContentType.Unknown;
                case StreamType.Audio:
                    return ContentType.Audio;
                case StreamType.Video:
                    return ContentType.Video;
                case StreamType.Subtitle:
                    return ContentType.Text;
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamType), streamType, null);
            }
        }

        public static ContentType ToContentType(this Tizen.TV.Multimedia.StreamType streamType)
        {
            switch (streamType)
            {
                case Tizen.TV.Multimedia.StreamType.Audio:
                    return ContentType.Audio;
                case Tizen.TV.Multimedia.StreamType.Video:
                    return ContentType.Video;
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamType), streamType, null);
            }
        }
    }

    public enum ContentType
    {
        Unknown,
        Video,
        Audio,
        Application,
        Text
    }
}
