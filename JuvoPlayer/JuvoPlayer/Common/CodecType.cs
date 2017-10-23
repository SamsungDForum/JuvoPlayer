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

namespace JuvoPlayer.Common {
    public enum VideoCodec {
        H264 = 1,
        VC1 = 2,
        MPEG2 = 3,
        MPEG4 = 4,
        THEORA = 5,
        VP8 = 6,
        VP9 = 7,
        H263 = 8,
        WMV1 = 9,
        WMV2 = 10,
        WMV3 = 11,
        INDEO3 = 12,
        H265 = 13,
    }

    public enum AudioCodec {
        AAC = 1,
        MP3 = 2,
        PCM = 3,
        VORBIS = 4,
        FLAC = 5,
        AMR_NB = 6,
        AMR_WB = 7,
        PCM_MULAW = 8,
        GSM_MS = 9,
        PCM_S16BE = 10,
        PCM_S24BE = 11,
        OPUS = 12,
        EAC3 = 13,
        MP2 = 14,
        DTS = 15,
        AC3 = 16,
        WMAV1 = 17,
        WMAV2 = 18,
    }
}
