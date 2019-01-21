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
using System.Linq;

namespace JuvoPlayer.Drms.Cenc
{
    internal static class CencUtils
    {
        public enum DrmType
        {
            PlayReady,
            Widevine,
            Unknown
        }

        public static readonly string PlayReadyType = "playready";
        public static readonly string PlayReadySchemeIdUri = "urn:uuid:9a04f079-9840-4286-ab92-e65be0885f95";
        public static readonly byte[] PlayReadySystemId = {0x9a, 0x04, 0xf0, 0x79, 0x98, 0x40, 0x42, 0x86,
                                                            0xab, 0x92, 0xe6, 0x5b, 0xe0, 0x88, 0x5f, 0x95, };

        public static readonly string WidevineType = "widevine";
        public static readonly string WidevineSchemeIdUri = "urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed";
        public static readonly byte[] WidevineSystemId = {0xed, 0xef, 0x8b, 0xa9, 0x79, 0xd6, 0x4a, 0xce,
                                                          0xa3, 0xc8, 0x27, 0xdc, 0xd5, 0x1d, 0x21, 0xed, };


        public static byte[] SchemeIdUriToSystemId(string schemeIdUri)
        {
            if (string.Equals(schemeIdUri, PlayReadySchemeIdUri, StringComparison.CurrentCultureIgnoreCase))
                return PlayReadySystemId;

            if (string.Equals(schemeIdUri, WidevineSchemeIdUri, StringComparison.CurrentCultureIgnoreCase))
                return WidevineSystemId;

            return null;
        }

        public static bool SupportsSchemeIdUri(string uri)
        {
            return string.Equals(uri, PlayReadySchemeIdUri, StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(uri, WidevineSchemeIdUri, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool SupportsSystemId(byte[] uuid)
        {
            return uuid.SequenceEqual(PlayReadySystemId)
                || uuid.SequenceEqual(WidevineSystemId);
        }

        public static bool SupportsType(string type)
        {
            return string.Equals(type, PlayReadyType, StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(type, WidevineType, StringComparison.CurrentCultureIgnoreCase);
        }

        public static string GetKeySystemName(byte[] systemId)
        {
            if (systemId.SequenceEqual(PlayReadySystemId))
                return "com.microsoft.playready";
            if (systemId.SequenceEqual(WidevineSystemId))
                return "com.widevine.alpha";

            return null;
        }

        public static string GetScheme(byte[] systemId)
        {
            if (systemId.SequenceEqual(PlayReadySystemId))
                return PlayReadyType;
            if (systemId.SequenceEqual(WidevineSystemId))
                return WidevineType;

            return null;
        }

        public static DrmType GetDrmType(string schemeName)
        {
            if (PlayReadyType == schemeName)
                return DrmType.PlayReady;

            if (WidevineType == schemeName)
                return DrmType.Widevine;

            return DrmType.Unknown;
        }
    }
}