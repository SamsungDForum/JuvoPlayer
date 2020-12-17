/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using System.Linq;
using System.Text;
using System.Xml.Linq;
using JuvoLogger;
using JuvoPlayer.Common;
using static JuvoPlayer.Utils.EndianTools;

namespace JuvoPlayer.Drms
{
    internal static class DrmInitDataTools
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        // Master KeyID is used as internal identifier of session which apply to entire
        // content but don't have any key ids defined.
        private static readonly byte[] MasterKid = Encoding.ASCII.GetBytes("OneToRuleThemAll");
        private static readonly int[] DashUuidFormat = { 4, 2, 2 };

        private static IEnumerable<byte[]> MicrosoftPlayReadyObjectHeader(byte[] data)
        {
            // PlayReady Header specs:
            // https://docs.microsoft.com/en-us/playready/specifications/playready-header-specification

            ushort playReadyObjectRecordCount = LittleEndian.AsUShort16(data, 4);

            var offset = 6;
            for (var i = 0; i < playReadyObjectRecordCount; i++)
            {
                ushort recordType = LittleEndian.AsUShort16(data, offset);
                ushort recordLength = LittleEndian.AsUShort16(data, offset + 2);

                // Reserved/Unspecified record type... ignoring
                if (recordType == 0 || recordType > 3)
                {
                    offset += recordLength;
                    continue;
                }

                var playReadyHeader = Encoding.Unicode.GetString(data, offset + 4, recordLength);
                var xDoc = XDocument.Parse(playReadyHeader);
                if (xDoc.Root == null)
                    continue;

                var kids = xDoc.Descendants(xDoc.Root.Name.Namespace + "KID");
                foreach (XElement kid in kids)
                {
                    Logger.Info(kid.Value);
                    // PlayReady KeyIds are in LE format. (each uuid block)
                    // Pssh box KeyIds are in BE format
                    //
                    // Use BE as common format - more human friendly - byte
                    // ordering as in cannonical mpd form
                    //
                    // UUID Note:
                    // Last 8 bytes of UUID are provided "as is" (i.e. BE form)
                    yield return 
                        LittleEndian.AsCanonicalUuid(Convert.FromBase64String(kid.Value), 0,DashUuidFormat);
                }
            }
        }

        private static IEnumerable<byte[]> PsshBox(byte[] pssh)
        {
            // Pssh box specs:
            // ISO IEC 23001-7 Cenc MP4

            // Check box type, 'pssh'
            if (pssh[4] != (byte)'p' ||
                pssh[5] != (byte)'s' ||
                pssh[6] != (byte)'s' ||
                pssh[7] != (byte)'h')
            {
                yield break;
            }

            // ISO IEC 23001-7 8.1.1
            // Check version. Must be > 0. Version 0 treat as:
            // (...) "Boxes without a list of applicable KID values, or with an empty list, 
            // SHALL be considered to apply to all KIDs in the file or movie fragment"
            if (pssh[8] == 0)
            {
                // push out master key id.
                yield return MasterKid;
                yield break;
            }

            uint kidCount = BigEndian.AsUInt32(pssh, 28);

            // 8.1.3 Semantics
            // KID identifies a key identifier that the Data field applies to. If not set, then the Data array SHALL
            // apply to all KIDs in the movie or movie fragment containing this box.
            if (kidCount == 0)
            {
                // push out master key id.
                yield return MasterKid;
                yield break;
            }

            var offset = 32;
            for (var i = 0; i < kidCount; i++)
            {
                var uuid = new byte[16];
                Array.ConstrainedCopy(pssh, offset, uuid, 0, 16);
                offset += 16;
                Logger.Info(KeyToUuid(uuid));
                yield return uuid;
            }
        }

        public static List<byte[]> GetKeyIds(DrmInitData initData)
        {
            var mpdSource = initData.KeyIDs?.Select(UuidToKey) ?? Enumerable.Empty<byte[]>();

            IEnumerable<byte[]> initDataSource;
            try
            {
                switch (initData.DataType)
                {
                    case DrmInitDataType.MsPrPro:
                        Logger.Info(initData.DataType.ToString());
                        initDataSource = MicrosoftPlayReadyObjectHeader(initData.InitData);
                        break;

                    case DrmInitDataType.Pssh:
                        Logger.Info(initData.DataType.ToString());
                        initDataSource = PsshBox(initData.InitData);
                        break;

                    default:
                        initDataSource = Enumerable.Empty<byte[]>();
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                // Init data seems malformed. May still usable by underlying decryption mechanisms.
                // Try processing what's available
                Logger.Warn("Possibly malformed DrmInitData.initData");
                initDataSource = Enumerable.Empty<byte[]>();
            }
            
            return mpdSource.Concat(initDataSource).ToList();
        }


        public static byte[] UuidToKey(string data)
        {
            // Remove hyphens
            data = data.Replace("-", "");

            // Make input data even length (should be anyway)
            int charCount = data.Length;
            if ((charCount & 0x01) == 0x01)
            {
                data += "0";
                charCount++;
            }

            int byteCount = charCount >> 1;
            byte[] result = new byte[byteCount];
            int charIdx = 0;

            // Grab nibble values
            // Convert them from string hex to value representation
            // merge and store
            for (int i = 0; i < byteCount; i++)
            {
                uint c = data[charIdx];
                uint highNibble = c & 0x40;
                highNibble = (((highNibble >> 3) | (highNibble >> 6)) + (c & 0x0F)) << 4;

                c = data[charIdx + 1];
                uint lowNibble = c & 0x40;
                lowNibble = ((lowNibble >> 3) | (lowNibble >> 6)) + (c & 0x0F);

                result[i] = (byte)(highNibble | lowNibble);
                charIdx += 2;
            }

            return result;
        }


        public static string KeyToUuid(byte[] key)
        {
            return new StringBuilder(35)
                .Append(BitConverter.ToString(key, 0, 4).Replace("-", ""))
                .Append('-')
                .Append(BitConverter.ToString(key, 4, 2).Replace("-", ""))
                .Append('-')
                .Append(BitConverter.ToString(key, 6, 2).Replace("-", ""))
                .Append('-')
                .Append(BitConverter.ToString(key, 8).Replace("-", ""))
                .ToString();
        }
    }
}
