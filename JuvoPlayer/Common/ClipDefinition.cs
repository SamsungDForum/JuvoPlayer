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
using System.Runtime.Serialization;

namespace JuvoPlayer.Common
{
    [DataContract]
    public class SubtitleInfo
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "language")]
        public string Language { get; set; }

        [DataMember(Name = "encoding")]
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

    [DataContract]
    public class DRMDescription
    {
        [DataMember(Name = "scheme")]
        public string Scheme { get; set; }

        [DataMember(Name = "licenseUrl")]
        public string LicenceUrl { get; set; }

        [DataMember(Name = "keyRequestProperties")]
        public Dictionary<string, string> KeyRequestProperties { get; set; }
    }

    [DataContract]
    public class ClipDefinition
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "subtitles")]
        public List<SubtitleInfo> Subtitles { get; set; }

        [DataMember(Name = "poster")]
        public string Poster { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "drmDatas")]
        public List<DRMDescription> DRMDatas { get; set; }
    }
}