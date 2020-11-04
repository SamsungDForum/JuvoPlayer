﻿/*!
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
using System.Collections.Generic;
using System.Linq;
using FFmpegBindings.Interop;
using JuvoLogger;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    internal class CodecExtraDataParser
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        internal static unsafe byte[] Parse(AVCodecID codecId, byte* extradata, int size)
        {
            switch (codecId)
            {
                case AVCodecID.AV_CODEC_ID_H264:
                    return ParseH264(extradata, size);
                case AVCodecID.AV_CODEC_ID_HEVC:
                    return ParseH265(extradata, size);
                default:
                    return null;
            }
        }

        // In case of H264 as VideoCodecConfig::extraData we will have avcC box, which
        // according to ISO/IEC 14496-15 chapter 5.3.3.1.2 has following structure:
        //
        // aligned(8) class AVCDecoderConfigurationRecord {
        //   unsigned int(8) configurationVersion = 1;
        //   unsigned int(8) AVCProfileIndication;
        //   unsigned int(8) profile_compatibility;
        //   unsigned int(8) AVCLevelIndication;
        //   bit(6) reserved = '111111'b;
        //   unsigned int(2) lengthSizeMinusOne;
        //   bit(3) reserved = '111'b;
        //   unsigned int(5) numOfSequenceParameterSets;
        //   for (i=0; i< numOfSequenceParameterSets; i++) {
        //     unsigned int(16) sequenceParameterSetLength ;
        //     bit(8*sequenceParameterSetLength) sequenceParameterSetNALUnit;
        //   }
        //   unsigned int(8) numOfPictureParameterSets;
        //   for (i=0; i< numOfPictureParameterSets; i++) {
        //     unsigned int(16) pictureParameterSetLength;
        //     bit(8*pictureParameterSetLength) pictureParameterSetNALUnit;
        //   }
        //   if( profile_idc == 100 || profile_idc == 110 ||
        //       profile_idc == 122 || profile_idc == 144 )
        //   {
        //     bit(6) reserved = '111111'b;
        //     unsigned int(2) chroma_format;
        //     bit(5) reserved = '11111'b;
        //     unsigned int(3) bit_depth_luma_minus8;
        //     bit(5) reserved = '11111'b;
        //     unsigned int(3) bit_depth_chroma_minus8;
        //     unsigned int(8) numOfSequenceParameterSetExt;
        //     for (i=0; i< numOfSequenceParameterSetExt; i++) {
        //       unsigned int(16) sequenceParameterSetExtLength;
        //       bit(8*sequenceParameterSetExtLength) sequenceParameterSetExtNALUnit;
        //     }
        //   }
        // }
        //
        // If we want to change representations with different codec extra
        // configuration in adaptive streaming scenarios, then we have to modify each
        // packet data by inserting before video samples SPSes and PPSes in the
        // following way:
        //   for each SPS:
        //     write length of SPS on lengthSizeMinusOne + 1 bytes in MSB format
        //       (BigEndian)
        //     write SPS NAL data (without any modifications)
        //   for each PPS: (do similar operation as for PPS)
        //     write length of PPS on lengthSizeMinusOne + 1 bytes in MSB format
        //     (BigEndian)
        //     write PPS NAL data (without any modifications)
        //   append video packet data
        //
        // For example:
        // - VideoCodecConfig::extra_data_:
        //       01 4D 40 20 FF E1 00 0C
        //       67 4D 40 20 96 52 80 A0 0B 76 02 05
        //       01 00 04 68 EF 38 80
        // - length_size: 4
        // - SPS count 1, SPS data: 67 4D 40 20 96 52 80 A0 0B 76 02 05
        // - PPS count 1, PPS data: 68 EF 38 80
        // - modified packet structure (in hex):
        //   00 00 00 0C 67 4D 40 20 96 52 80 A0 0B 76 02 05 00 00 00 04 68 EF 38 80
        //  |           |                                   |           |
        //  |           |                                   |           |
        //  |           |                                   |           | PPS NAL
        //  |           |                                   |
        //  |           |                                   | PPS length (4 bytes)
        //  |           |
        //  |           | SPS NAL
        //  |
        //  | SPS length (4 bytes)
        //  after that header original ES packet bytes are appended
        private static unsafe byte[] ParseH264(byte* extraData, int extraDataSize)
        {
            if (extraDataSize < 6)
            {
                // Min first 5 byte + num_sps
                Logger.Error("extra_data is too short to pass valid SPS/PPS header");
                return null;
            }

            var idx = 0;
            var version = ReadByte(extraData, ref idx);
            var profileIndication = ReadByte(extraData, ref idx);
            var profileCompatibility = ReadByte(extraData, ref idx);
            var avcLevel = ReadByte(extraData, ref idx);

            uint lengthSize = ReadByte(extraData, ref idx);
            if ((lengthSize & 0xFCu) != 0xFCu)
            {
                // Be liberal in what you accept..., so just log error
                Logger.Warn("Not all reserved bits in length size filed are set to 1");
            }

            lengthSize = (byte) ((lengthSize & 0x3u) + 1);

            uint numSps = ReadByte(extraData, ref idx);
            if ((numSps & 0xE0u) != 0xE0u)
            {
                // Be liberal in what you accept..., so just log error
                Logger.Warn("Wrong SPS count format.");
            }

            numSps &= 0x1Fu;

            var spses = ReadH264ParameterSets(extraData, extraDataSize, numSps, ref idx);
            if (spses == null)
            {
                Logger.Error("extra data too short");
                return null;
            }

            if (extraDataSize <= idx)
            {
                Logger.Error("extra data too short");
                return null;
            }

            uint numPps = ReadByte(extraData, ref idx);

            var ppses = ReadH264ParameterSets(extraData, extraDataSize, numPps, ref idx);
            if (ppses == null)
            {
                Logger.Error("extra data too short");
                return null;
            }

            var size = spses.Sum(o => lengthSize + o.Length)
                       + ppses.Sum(o => lengthSize + o.Length);

            var parsedExtraData = new byte[size];
            var offset = 0;

            CopySet(lengthSize, spses, parsedExtraData, ref offset);
            CopySet(lengthSize, ppses, parsedExtraData, ref offset);
            return parsedExtraData;
        }

        private static unsafe byte ReadByte(byte* adata, ref int idx)
        {
            return adata[idx++];
        }

        private static unsafe UInt16 ReadUInt16(byte* adata, ref int idx)
        {
            ushort res = 0;
            for (var i = 0; i < 2; ++i)
            {
                res <<= 8;
                res |= adata[idx + i];
            }

            idx += 2;
            return res;
        }

        private static unsafe List<byte[]> ReadH264ParameterSets(byte* extraData, int extraDataSize, uint count,
            ref int idx)
        {
            var sets = new List<byte[]>();
            for (var i = 0; i < count; i++)
            {
                if (extraDataSize < idx + 2)
                {
                    Logger.Error("extra data too short");
                    return null;
                }

                uint length = ReadUInt16(extraData, ref idx);
                if (extraDataSize < idx + length)
                {
                    Logger.Error("extra data too short");
                    return null;
                }

                sets.Add(new Span<byte>(extraData + idx, (int) length).ToArray());
                idx += (int) length;
            }

            return sets;
        }

        private static void CopySet(uint lengthSize, IEnumerable<byte[]> nals, byte[] parsedExtraData, ref int offset)
        {
            foreach (var pps in nals)
            {
                var len = AsBytesMSB((uint) pps.Length, (int) lengthSize);
                Buffer.BlockCopy(len, 0, parsedExtraData, offset, len.Length);
                offset += len.Length;
                Buffer.BlockCopy(pps, 0, parsedExtraData, offset, pps.Length);
                offset += pps.Length;
            }
        }

        private static byte[] AsBytesMSB(uint val, int maxBytes)
        {
            var ret = new byte[maxBytes];

            for (--maxBytes; maxBytes >= 0 && val > 0; --maxBytes)
            {
                ret[maxBytes] = (byte) (val & 0xFFu);
                val >>= 8;
            }

            return ret;
        }

        // In case of H265 as VideoCodecConfig::extraData we will have box, which
        // according to ISO/IES 14496-15 chapter 8.3.3.1.2 has following structure:
        //
        // aligned(8) class HEVCDecoderConfigurationRecord {
        //   unsigned int(8) configurationVersion = 1;
        //   unsigned int(2) general_profile_space;
        //   unsigned int(1) general_tier_flag;
        //   unsigned int(5) general_profile_idc;
        //   unsigned int(32) general_profile_compatibility_flags;
        //   unsigned int(48) general_constraint_indicator_flags;
        //   unsigned int(8) general_level_idc;
        //   bit(4) reserved = ‘1111’b;
        //   unsigned int(12) min_spatial_segmentation_idc;
        //   bit(6) reserved = ‘111111’b;
        //   unsigned int(2) parallelismType;
        //   bit(6) reserved = ‘111111’b;
        //   unsigned int(2) chromaFormat;
        //   bit(5) reserved = ‘11111’b;
        //   unsigned int(3) bitDepthLumaMinus8;
        //   bit(5) reserved = ‘11111’b;
        //   unsigned int(3) bitDepthChromaMinus8;
        //   bit(16) avgFrameRate;
        //   bit(2) constantFrameRate;
        //   bit(3) numTemporalLayers;
        //   bit(1) temporalIdNested;
        //   unsigned int(2) lengthSizeMinusOne;
        //   unsigned int(8) numOfArrays;
        //   for (j=0; j < numOfArrays; j++) {
        //     bit(1) array_completeness;
        //     unsigned int(1) reserved = 0;
        //     unsigned int(6) NAL_unit_type;
        //     unsigned int(16) numNalus;
        //       for (i=0; i< numNalus; i++) {
        //         unsigned int(16) nalUnitLength;
        //         bit(8*nalUnitLength) nalUnit;
        //       }
        //     }
        //   }
        //
        // "NAL_unit_type indicates the type of the NAL units in the following array
        // (which must be all of that type); it takes a value as defined
        // in ISO/IEC 23008‐2; it is restricted to take one of the
        // values indicating a VPS, SPS, PPS, or SEI NAL unit;"
        // (from ISO/IES 14496-15 chapter 8.3.3.1.2)
        //
        // If we want to change representations with different codec extra
        // configuration in adaptive streaming scenarios, then we have to modify each
        // packet data by inserting before video samples extracted NALs in the
        // following way:
        //   for each nalUnit:
        //     write nalUnitLength on lengthSizeMinusOne + 1 bytes in MSB format
        //       (BigEndian)
        //     write nalUnit (without any modifications)
        //   append video packet data
        private static unsafe byte[] ParseH265(byte* extraData, int extraDataSize)
        {
            if (extraDataSize < 21)
            {
                // Min first 5 byte + num_sps
                Logger.Error("extra_data is too short to pass valid SPS/PPS header");
                return null;
            }

            var idx = 21;
            var lengthSize = (ReadByte(extraData, ref idx) & 0x3u) + 1;
            var numOfArrays = ReadByte(extraData, ref idx);

            var nals = new List<byte[]>();
            for (var j = 0; j < numOfArrays; ++j)
            {
                if (extraDataSize < idx + 3)
                {
                    Logger.Error("extra data too short");
                    return null;
                }

                var nalUnitType = ReadByte(extraData, ref idx) & 0x3Fu;
                var numNalus = ReadUInt16(extraData, ref idx);
                for (var i = 0; i < numNalus; ++i)
                {
                    if (extraDataSize < idx + 2)
                    {
                        Logger.Error("extra data too short");
                        return null;
                    }

                    var nalUnitLength = ReadUInt16(extraData, ref idx);
                    if (extraDataSize < idx + nalUnitLength)
                    {
                        Logger.Error("extra data too short");
                        return null;
                    }

                    nals.Add(new Span<byte>(extraData + idx, nalUnitLength).ToArray());
                    idx += nalUnitLength;
                }
            }

            var size = nals.Sum(o => lengthSize + o.Length);

            var parsedExtraData = new byte[size];

            var offset = 0;
            CopySet(lengthSize, nals, parsedExtraData, ref offset);
            return parsedExtraData;
        }
    }
}