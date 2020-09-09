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
using System.Collections.Generic;

namespace JuvoPlayer.Common
{
    public class SubtitleInfo
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string Language { get; set; }
        public string Encoding { get; set; }
        public string MimeType { get; set; }

        public StreamDescription ToStreamDescription()
        {
            return new StreamDescription()
            {
                Description = Language,
                Id = Id,
                StreamType = StreamType.Subtitle
            };
        }

        public SubtitleInfo()
        { }

        public SubtitleInfo(SubtitleInfo createFrom)
        {
            Id = createFrom.Id;
            Path = createFrom.Path;
            Language = createFrom.Language;
            Encoding = createFrom.Encoding;
            MimeType = createFrom.MimeType;
        }

    }

    public class DrmDescription
    {
        public string Scheme { get; set; }
        public string LicenceUrl { get; set; }
        public Dictionary<string, string> KeyRequestProperties { get; set; }
        public bool IsImmutable { get; set; }
    }

    public class ClipDefinition
    {
        public enum VideoFormat : uint
        {
            Custom = 0,

            i480 = 720 * 480,
            i576 = 720 * 576,
            i1080 = 1920 * 1080,

            p240 = 426 * 240,
            p360 = 640 * 360,
            p480 = 854 * 480,
            p720 = 1280 * 720,
            p1080 = 1920 * 1080,
            p1440 = 2560 * 1440,
            p2160 = 3840 * 2160,
            p4320 = 7680 * 4320,

            HD1 = p720,
            HDReady = p720,
            HD2 = p1080,
            FullHD = p1080,

            UHD = 3840 * 2160,
            UHD1 = 3840 * 2160,
            UHD4K = 3840 * 2160,
            UHDTV1 = 3840 * 2160,
            DCI = 4096 * 2160,
            DCI4K = 4096 * 2160,

            UW5K = 5120 * 2160,

            UHD2 = 7680 * 4320,
            UHD8K = 7680 * 4320,
            UHDTV2 = 7680 * 4320,
            SuperHiVision = 7680 * 4320,
            DCI8K = 8192 * 4320,

            UW10K = 10240 * 4320
        }

        public string Title { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public List<SubtitleInfo> Subtitles { get; set; }
        public string Poster { get; set; }
        public string Description { get; set; }
        public List<DrmDescription> DRMDatas { get; set; }
        public string SeekPreviewPath { get; set; }
        public string TilePreviewPath { get; set; }

        private VideoFormat _videoFormat = VideoFormat.Custom;

        public uint PixelCount { get; private set; } = 0;

        public VideoFormat Format
        {
            get => _videoFormat;
            set
            {
                _videoFormat = value;
                PixelCount = (uint)value;
            }
        }

        public uint[] Resolution
        {
            set
            {
                if (value.Length != 2)
                    throw new ArgumentException($"{nameof(Resolution)} requires exactly 2 values");

                PixelCount = value[0] * value[1];
                _videoFormat = Enum.IsDefined(typeof(VideoFormat), PixelCount)
                    ? (VideoFormat)PixelCount
                    : VideoFormat.Custom;
            }
        }
    }
}