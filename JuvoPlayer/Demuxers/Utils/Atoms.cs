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
using System.IO;
using JuvoLogger;

namespace JuvoPlayer.Demuxers.Utils
{
    public abstract class AtomBase
    {
        protected static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        protected uint atomSize;

        public abstract void ParseAtom(byte[] adata, ulong dataStart);

        protected static T Read<T>(byte[] adata, ref int idx)
        {
            T res = default;

            // sizeof(T) on template does not work
            // SizeOf(T) (dynamic) apparently is not reliable - may return size of entire
            // container ther then storage size...
            // ...Surely, there must be more more sensible way of doing this...
            switch (Type.GetTypeCode(res.GetType()))
            {
                case TypeCode.UInt32:
                    if (BitConverter.IsLittleEndian) Array.Reverse(adata, idx, 4);
                    res = (T) Convert.ChangeType(BitConverter.ToUInt32(adata, idx), res.GetType());
                    idx += 4;
                    break;
                case TypeCode.Int32:
                    if (BitConverter.IsLittleEndian) Array.Reverse(adata, idx, 4);
                    res = (T) Convert.ChangeType(BitConverter.ToInt32(adata, idx), res.GetType());
                    idx += 4;
                    break;
                case TypeCode.UInt64:
                    if (BitConverter.IsLittleEndian) Array.Reverse(adata, idx, 8);
                    res = (T) Convert.ChangeType(BitConverter.ToUInt64(adata, idx), res.GetType());
                    idx += 8;
                    break;
                case TypeCode.Int64:
                    if (BitConverter.IsLittleEndian) Array.Reverse(adata, idx, 8);
                    res = (T) Convert.ChangeType(BitConverter.ToInt64(adata, idx), res.GetType());
                    idx += 8;
                    break;
                case TypeCode.UInt16:
                    if (BitConverter.IsLittleEndian) Array.Reverse(adata, idx, 2);
                    res = (T) Convert.ChangeType(BitConverter.ToUInt16(adata, idx), res.GetType());
                    idx += 2;
                    break;
                case TypeCode.Int16:
                    if (BitConverter.IsLittleEndian) Array.Reverse(adata, idx, 2);
                    res = (T) Convert.ChangeType(BitConverter.ToInt16(adata, idx), res.GetType());
                    idx += 2;
                    break;
                case TypeCode.Byte:
                    res = (T) Convert.ChangeType(adata[idx++], res.GetType());
                    break;
                // fallthrough are intentional here...
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.DBNull:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Empty:
                case TypeCode.Object:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.String:
                default:
                    Logger.Warn($"{res.GetType()} Unsupported read type.");
                    break;
            }

            return res;
        }

        protected static bool CheckName(byte[] adata, ref int idx, byte[] name)
        {
            if (adata.Length - idx < 4) return false;

            // Check signature.
            if (!(name[0] == adata[idx++] &&
                  name[1] == adata[idx++] &&
                  name[2] == adata[idx++] &&
                  name[3] == adata[idx++]))
                return false;

            return true;
        }

        protected static double ToMilliseconds(ulong val, uint scale)
        {
            return val * 1000 / (double) scale;
        }
    }

    public class SidxAtom : AtomBase
    {
        protected static readonly byte[] AtomName = {(byte) 's', (byte) 'i', (byte) 'd', (byte) 'x'};
        protected readonly byte[] Flags = new byte[3];
        protected byte version;

        public SidxAtom()
        {
            Movieidx = new List<MovieIndexEntry>();
            Sidxidx = new List<SidxIndexEntry>();
        }

        public SidxAtom(byte[] adata, ulong dataStart)
        {
            Movieidx = new List<MovieIndexEntry>();
            Sidxidx = new List<SidxIndexEntry>();

            ParseAtom(adata, dataStart);
        }

        protected ushort Reserved { get; set; }

        // Movieidx contains a list of content index entries
        // from single SIDX Atom.
        private List<MovieIndexEntry> Movieidx { get; }
        private List<SidxIndexEntry> Sidxidx { get; }

        public uint MovieIndexCount => (uint) Movieidx.Count;

        public uint SidxIndexCount => (uint) Sidxidx.Count;
        public uint ReferenceId { get; set; }
        public uint Timescale { get; set; }
        public ulong RawPts { get; set; }
        public ulong RawOffset { get; set; }
        public TimeSpan AverageSegmentDuration { get; set; }

        public TimeSpan MaxIndexTime { get; set; }

        public (ulong, ulong, TimeSpan, TimeSpan) GetRangeData(uint idx)
        {
            ulong rl = 0;
            ulong rh = 0;
            TimeSpan startTime = default;
            TimeSpan duration = default;

            if (idx < MovieIndexCount)
            {
                rl = Movieidx[(int) idx].Offset;
                rh = rl + Movieidx[(int) idx].RawRefsize;
                startTime = Movieidx[(int) idx].TimeIndex;
                duration = Movieidx[(int) idx].SegmentDuration;
            }

            return (rl, rh, startTime, duration);
        }

        public override void ParseAtom(byte[] adata, ulong dataStart)
        {
            var idx = 0;

            atomSize = Read<uint>(adata, ref idx);

            // Sanity Check
            if (atomSize > adata.Length)
                throw new InvalidDataException("SIDX buffer shorter then indicated by atom size.");
            // Check signature
            if (CheckName(adata, ref idx, AtomName) == false)
                throw new InvalidDataException("Missing SIDX atom header.");

            version = Read<byte>(adata, ref idx);

            // Flags are only 3 bytes. Do byte at a time
            // as read will swap bytes around...
            Flags[0] = Read<byte>(adata, ref idx);
            Flags[1] = Read<byte>(adata, ref idx);
            Flags[2] = Read<byte>(adata, ref idx);

            ReferenceId = Read<uint>(adata, ref idx);

            Timescale = Read<uint>(adata, ref idx);

            ulong pts = 0;
            var offset = dataStart;

            if (version == 0)
            {
                RawPts = Read<uint>(adata, ref idx);
                RawOffset = Read<uint>(adata, ref idx);
            }
            else
            {
                RawPts = Read<ulong>(adata, ref idx);
                RawOffset = Read<ulong>(adata, ref idx);
            }

            pts += RawPts;
            offset += RawOffset;

            Reserved = Read<ushort>(adata, ref idx);

            var referenceCount = Read<ushort>(adata, ref idx);

            var avgSegDur = 0.0;
            var i = 1;
            uint indexCount = 0;

            Movieidx.Clear();
            Sidxidx.Clear();

            while (referenceCount-- > 0)
            {
                var refSize = Read<uint>(adata, ref idx);

                var typeset = (refSize & 0x80000000) > 0;

                refSize &= 0x7FFFFFF;

                var ssegDuration = Read<uint>(adata, ref idx);
                var saPdata = Read<uint>(adata, ref idx);

                var currentDuration = ToMilliseconds(ssegDuration, Timescale);

                avgSegDur = (currentDuration - avgSegDur) / i;
                i++;

                // When storing sizes (ref_size), we need to store value -1 byte,
                // as ref size is offset to the first byte of next reference material.
                // ISO_IEC_14496-12_2015.pdf Section 8.16.3.3
                // Since we are using it for range info (which is inclusive) we need
                // to stop a byte before next reference content.
                if (typeset)
                {
                    Sidxidx.Add(
                        new SidxIndexEntry(refSize - 1, ssegDuration, saPdata, offset));
                }
                else
                {
                    Movieidx.Add(
                        new MovieIndexEntry(refSize - 1, ssegDuration, saPdata, offset,
                            TimeSpan.FromMilliseconds(ToMilliseconds(ssegDuration, Timescale)),
                            TimeSpan.FromMilliseconds(ToMilliseconds(pts, Timescale)),
                            indexCount++));
                }

                pts += ssegDuration;

                offset += refSize;

                // Assign max time contained within this particular SIDX Atom.
                // This is the last entry in SIDX box + its duration
                MaxIndexTime = TimeSpan.FromMilliseconds(ToMilliseconds(pts, Timescale));
            }

            AverageSegmentDuration = TimeSpan.FromMilliseconds(avgSegDur);
        }

        public class SidxIndexEntry
        {
            public SidxIndexEntry(uint refsize, uint duration, uint sapdata,
                ulong offdata)
            {
                RawRefsize = refsize;
                RawDuration = duration;
                SapData = sapdata;
                Offset = offdata;
            }

            public uint RawRefsize { get; }
            public uint RawDuration { get; }

            public uint SapData { get; }

            public ulong Offset { get; }
        }

        public class MovieIndexEntry : SidxIndexEntry
        {
            public MovieIndexEntry(uint rawRefsize, uint rawDuration, uint sap,
                ulong offdata, TimeSpan segmentDuration, TimeSpan timeIndex, uint id)
                : base(rawRefsize, rawDuration, sap, offdata)
            {
                SegmentDuration = segmentDuration;
                TimeIndex = timeIndex;
                Id = id;
            }

            public TimeSpan SegmentDuration { get; }
            public TimeSpan TimeIndex { get; }
            public uint Id { get; }
        }
    }
}