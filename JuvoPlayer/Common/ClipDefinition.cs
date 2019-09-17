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

    public class DRMDescription
    {
        public string Scheme { get; set; }
        public string LicenceUrl { get; set; }
        public Dictionary<string, string> KeyRequestProperties { get; set; }
        public bool IsImmutable { get; set; }
    }

    public class ClipDefinition
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public List<SubtitleInfo> Subtitles { get; set; }
        public string Poster { get; set; }
        public string Description { get; set; }
        public List<DRMDescription> DRMDatas { get; set; }
    }
}