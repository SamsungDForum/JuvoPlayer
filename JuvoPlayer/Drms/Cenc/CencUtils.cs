using System;
using System.Linq;

namespace JuvoPlayer.Drms.Cenc
{
    internal static class CencUtils
    {
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
                /*|| string.Equals(uri, WidevineSchemeIdUri, StringComparison.CurrentCultureIgnoreCase)*/;
        }

        public static bool SupportsSystemId(byte[] uuid)
        {
            return uuid.SequenceEqual(PlayReadySystemId)
               /* || uuid.SequenceEqual(WidevineSystemId)*/;
        }

        public static bool SupportsType(string type)
        {
            return string.Equals(type, PlayReadyType, StringComparison.CurrentCultureIgnoreCase)
                /*|| string.Equals(type, WidevineType, StringComparison.CurrentCultureIgnoreCase)*/;
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
                return "playready";
            if (systemId.SequenceEqual(WidevineSystemId))
                return "widevine";

            return null;
        }
    }
}
