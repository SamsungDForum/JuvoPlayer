/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using System.Linq;
using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public static class EmeUtils
    {
        private static readonly string[] KeyIdSeparators = { " " };

        public enum DrmType
        {
            Unknown,
            PlayReady,
            Widevine
        }

        private struct DrmConstants
        {
            public DrmType Type;
            public string KeySystemName;
            public string SchemeIdUri;
            public byte[] SystemId;
        }

        private static DrmConstants PlayReadyConstants = new DrmConstants
        {
            Type = DrmType.PlayReady,
            KeySystemName = "com.microsoft.playready",
            SchemeIdUri = "urn:uuid:9a04f079-9840-4286-ab92-e65be0885f95",
            SystemId = new byte[]
            {
                0x9a, 0x04, 0xf0, 0x79, 0x98, 0x40, 0x42, 0x86,
                0xab, 0x92, 0xe6, 0x5b, 0xe0, 0x88, 0x5f, 0x95,
            }
        };

        private static DrmConstants WidevineConstants = new DrmConstants
        {
            Type = DrmType.Widevine,
            KeySystemName = "com.widevine.alpha",
            SchemeIdUri = "urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed",
            SystemId = new byte[]
            {
                0xed, 0xef, 0x8b, 0xa9, 0x79, 0xd6, 0x4a, 0xce,
                0xa3, 0xc8, 0x27, 0xdc, 0xd5, 0x1d, 0x21, 0xed,
            }
        };

        private static DrmConstants[] AllDrmConstants = { PlayReadyConstants, WidevineConstants };

        public static byte[] SchemeIdUriToSystemId(string schemeIdUri)
        {
            return AllDrmConstants.FirstOrDefault(a =>
                    string.Equals(a.SchemeIdUri, schemeIdUri, StringComparison.CurrentCultureIgnoreCase))
                .SystemId;
        }

        public static bool SupportsSchemeIdUri(string uri)
        {
            return AllDrmConstants.Any(a =>
                string.Equals(a.SchemeIdUri, uri, StringComparison.CurrentCultureIgnoreCase));
        }

        public static bool SupportsSystemId(byte[] uuid)
        {
            return AllDrmConstants.Any(a =>
                a.SystemId.SequenceEqual(uuid));
        }

        public static bool SupportsType(string type)
        {
            return AllDrmConstants.Any(a =>
                string.Equals(a.Type.ToString(), type, StringComparison.CurrentCultureIgnoreCase));
        }

        public static string GetKeySystemName(byte[] systemId)
        {
            return AllDrmConstants.FirstOrDefault(a => a.SystemId.SequenceEqual(systemId)).KeySystemName;
        }

        public static string GetScheme(byte[] systemId)
        {
            return AllDrmConstants.FirstOrDefault(a => a.SystemId.SequenceEqual(systemId)).Type.ToString()
                .ToLowerInvariant();
        }

        public static DrmType GetDrmType(string schemeName)
        {
            return AllDrmConstants.FirstOrDefault(a =>
                string.Equals(a.Type.ToString(), schemeName, StringComparison.CurrentCultureIgnoreCase)).Type;
        }

        public static DrmType GetDrmTypeFromKeySystemName(string keySystemName)
        {
            return AllDrmConstants.FirstOrDefault(a =>
                string.Equals(a.KeySystemName, keySystemName, StringComparison.CurrentCultureIgnoreCase)).Type;
        }

        public static DrmInitDataType GetInitDataType(string initDataName)
        {
            if (initDataName == null)
                return DrmInitDataType.Unknown;

            var initName = initDataName.ToLowerInvariant();

            switch (initName)
            {
                case "mspr:pro":
                    return DrmInitDataType.MsPrPro;

                case "cenc:pssh":
                    return DrmInitDataType.Pssh;

                default:
                    return DrmInitDataType.Unknown;
            }
        }

        public static string[] GetKeyIDs(string source)
        {
            return source?.Split(KeyIdSeparators, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}