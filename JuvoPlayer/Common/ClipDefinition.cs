// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

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