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
    public class MimeType
    {
        public static readonly string BaseTypeVideo = "video";
        public static readonly string BaseTypeAudio = "audio";
        public static readonly string BaseTypeText = "text";
        public static readonly string BaseTypeApplication = "application";

        public static readonly string VideoMp4 = BaseTypeVideo + "/mp4";
        public static readonly string VideoWebm = BaseTypeVideo + "/webm";
        public static readonly string VideoH263 = BaseTypeVideo + "/3gpp";
        public static readonly string VideoH264 = BaseTypeVideo + "/avc";
        public static readonly string VideoH265 = BaseTypeVideo + "/hevc";
        public static readonly string VideoVp8 = BaseTypeVideo + "/x-vnd.on2.vp8";
        public static readonly string VideoVp9 = BaseTypeVideo + "/x-vnd.on2.vp9";
        public static readonly string VideoAv1 = BaseTypeVideo + "/av01";
        public static readonly string VideoMp4V = BaseTypeVideo + "/mp4v-es";
        public static readonly string VideoMpeg = BaseTypeVideo + "/mpeg";
        public static readonly string VideoMpeg2 = BaseTypeVideo + "/mpeg2";
        public static readonly string VideoVc1 = BaseTypeVideo + "/wvc1";
        public static readonly string VideoDolbyVision = BaseTypeVideo + "/dolby-vision";
        public static readonly string VideoTheora = BaseTypeVideo + "/theora";
        public static readonly string VideoWmv = BaseTypeVideo + "/x-ms-wmv";

        public static readonly string AudioMp3 = BaseTypeAudio + "/mp3";
        public static readonly string AudioMp4 = BaseTypeAudio + "/mp4";
        public static readonly string AudioAac = BaseTypeAudio + "/mp4a-latm";
        public static readonly string AudioWebm = BaseTypeAudio + "/webm";
        public static readonly string AudioMpeg = BaseTypeAudio + "/mpeg";
        public static readonly string AudioRaw = BaseTypeAudio + "/raw";
        public static readonly string AudioAc3 = BaseTypeAudio + "/ac3";
        public static readonly string AudioEac3 = BaseTypeAudio + "/eac3";
        public static readonly string AudioEac3Joc = BaseTypeAudio + "/eac3-joc";
        public static readonly string AudioAc4 = BaseTypeAudio + "/ac4";
        public static readonly string AudioDts = BaseTypeAudio + "/vnd.dts";
        public static readonly string AudioDtsHd = BaseTypeAudio + "/vnd.dts.hd";
        public static readonly string AudioVorbis = BaseTypeAudio + "/vorbis";
        public static readonly string AudioOpus = BaseTypeAudio + "/opus";
        public static readonly string AudioFlac = BaseTypeAudio + "/flac";

        public static readonly string TextVtt = BaseTypeText + "/vtt";

        public static readonly string ApplicationMp4 = BaseTypeApplication + "/mp4";
        public static readonly string ApplicationRawCc = BaseTypeApplication + "/x-rawcc";
        public static readonly string ApplicationTtml = BaseTypeApplication + "/ttml+xml";
        public static readonly string ApplicationMp4Vtt = BaseTypeApplication + "/x-mp4-vtt";
        public static readonly string ApplicationCea608 = BaseTypeApplication + "/cea-608";
        public static readonly string ApplicationCea708 = BaseTypeApplication + "/cea-708";

        private static string GetTopLevelType(string mimeType)
        {
            if (mimeType == null)
                return null;
            var indexOfSlash = mimeType.IndexOf('/');
            if (indexOfSlash == -1)
                return null;
            return mimeType.Substring(0, indexOfSlash);
        }

        public static bool IsVideo(string mimeType)
        {
            return BaseTypeVideo.Equals(GetTopLevelType(mimeType));
        }

        public static bool IsAudio(string mimeType)
        {
            return BaseTypeAudio.Equals(GetTopLevelType(mimeType));
        }

        public static bool IsApplication(string mimeType)
        {
            return BaseTypeApplication.Equals(GetTopLevelType(mimeType));
        }

        public static bool IsText(string mimeType)
        {
            return BaseTypeText.Equals(GetTopLevelType(mimeType));
        }

        public static string GetAudioMediaMimeType(string codecs)
        {
            if (codecs == null)
                return null;
            var codecsList = codecs.Trim().Split(',');
            foreach (var codec in codecsList)
            {
                var mimeType = GetMediaMimeType(codec);
                if (mimeType != null && mimeType.StartsWith(BaseTypeAudio))
                    return mimeType;
            }

            return null;
        }

        public static string GetVideoMediaMimeType(string codecs)
        {
            if (codecs == null)
                return null;
            var codecsList = codecs.Trim().Split(',');
            foreach (var codec in codecsList)
            {
                var mimeType = GetMediaMimeType(codec);
                if (mimeType != null && mimeType.StartsWith(BaseTypeVideo))
                    return mimeType;
            }

            return null;
        }

        public static string GetMediaMimeType(string codec)
        {
            if (codec == null)
                return null;
            codec = codec.Trim().ToLowerInvariant();
            if (codec.StartsWith("avc1") || codec.StartsWith("avc3")) return VideoH264;

            if (codec.StartsWith("hev1") || codec.StartsWith("hvc1")) return VideoH265;

            if (codec.StartsWith("dvav") || codec.StartsWith("dva1") || codec.StartsWith("dvhe") ||
                codec.StartsWith("dvh1"))
                return VideoDolbyVision;

            if (codec.StartsWith("av01")) return VideoAv1;

            if (codec.StartsWith("vp9") || codec.StartsWith("vp09")) return VideoVp9;

            if (codec.StartsWith("vp8") || codec.StartsWith("vp08")) return VideoVp8;

            if (codec.StartsWith("mp4a"))
            {
                string mimeType = null;
                var objectTypeString = codec.Substring(5);
                if (objectTypeString.Length >= 2)
                {
                    try
                    {
                        var objectTypeHexString = objectTypeString.ToUpperInvariant().Substring(0, 2);
                        var objectTypeInt = Convert.ToInt32(objectTypeHexString, 16);
                        mimeType = GetMimeTypeFromMp4ObjectType(objectTypeInt);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                return mimeType ?? AudioAac;
            }

            if (codec.StartsWith("ac-3") || codec.StartsWith("dac3"))
                return AudioAc3;
            if (codec.StartsWith("ec-3") || codec.StartsWith("dec3"))
                return AudioEac3;
            if (codec.StartsWith("ec+3"))
                return AudioEac3Joc;
            if (codec.StartsWith("ac-4") || codec.StartsWith("dac4"))
                return AudioAc4;
            if (codec.StartsWith("dtsc") || codec.StartsWith("dtse"))
                return AudioDts;
            if (codec.StartsWith("dtsh") || codec.StartsWith("dtsl"))
                return AudioDtsHd;
            if (codec.StartsWith("opus"))
                return AudioOpus;
            if (codec.StartsWith("vorbis"))
                return AudioVorbis;
            if (codec.StartsWith("flac"))
                return AudioFlac;
            if (codec.StartsWith("stpp"))
                return ApplicationTtml;
            if (codec.StartsWith("wvtt"))
                return TextVtt;
            return null;
        }

        private static string GetMimeTypeFromMp4ObjectType(int objectType)
        {
            switch (objectType)
            {
                case 0x20:
                    return VideoMp4V;
                case 0x21:
                    return VideoH264;
                case 0x23:
                    return VideoH265;
                case 0x60:
                case 0x61:
                case 0x62:
                case 0x63:
                case 0x64:
                case 0x65:
                    return VideoMpeg2;
                case 0x6A:
                    return VideoMpeg;
                case 0x69:
                case 0x6B:
                    return AudioMpeg;
                case 0xA3:
                    return VideoVc1;
                case 0xB1:
                    return VideoVp9;
                case 0x40:
                case 0x66:
                case 0x67:
                case 0x68:
                    return AudioAac;
                case 0xA5:
                    return AudioAc3;
                case 0xA6:
                    return AudioEac3;
                case 0xA9:
                case 0xAc:
                    return AudioDts;
                case 0xAA:
                case 0xAB:
                    return AudioDtsHd;
                case 0xAD:
                    return AudioOpus;
                case 0xAE:
                    return AudioAc4;
                default:
                    return null;
            }
        }
    }
}
