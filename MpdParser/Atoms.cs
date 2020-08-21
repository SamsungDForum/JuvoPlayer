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

namespace MpdParser.Node.Atom
{
    public abstract class AtomBase
    {
        protected UInt32 AtomSize;
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        public abstract void ParseAtom(byte[] adata, ulong dataStart);

        protected static T Read<T>(byte[] adata, ref int idx)
        {
            T res = default(T);

            // sizeof(T) on template does not work
            // SizeOf(T) (dynamic) apparently is not reliable - may return size of entire
            // container ther then storage size...
            // ...Surely, there must be more more sensible way of doing this...
            switch (Type.GetTypeCode(res.GetType()))
            {
                case TypeCode.UInt32:
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(adata, idx, 4);
                    }
                    res = (T)Convert.ChangeType(BitConverter.ToUInt32(adata, idx), res.GetType());
                    idx += 4;
                    break;
                case TypeCode.Int32:
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(adata, idx, 4);
                    }
                    res = (T)Convert.ChangeType(BitConverter.ToInt32(adata, idx), res.GetType());
                    idx += 4;
                    break;
                case TypeCode.UInt64:
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(adata, idx, 8);
                    }
                    res = (T)Convert.ChangeType(BitConverter.ToUInt64(adata, idx), res.GetType());
                    idx += 8;
                    break;
                case TypeCode.Int64:
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(adata, idx, 8);
                    }
                    res = (T)Convert.ChangeType(BitConverter.ToInt64(adata, idx), res.GetType());
                    idx += 8;
                    break;
                case TypeCode.UInt16:
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(adata, idx, 2);
                    }
                    res = (T)Convert.ChangeType(BitConverter.ToUInt16(adata, idx), res.GetType());
                    idx += 2;
                    break;
                case TypeCode.Int16:
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(adata, idx, 2);
                    }
                    res = (T)Convert.ChangeType(BitConverter.ToInt16(adata, idx), res.GetType());
                    idx += 2;
                    break;
                case TypeCode.Byte:
                    res = (T)Convert.ChangeType(adata[idx++], res.GetType());
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
            if ((adata.Length - idx) < 4)
            {
                return false;
            }

            // Check signature.
            if (!(name[0] == adata[idx++] &&
                    name[1] == adata[idx++] &&
                    name[2] == adata[idx++] &&
                    name[3] == adata[idx++]))
            {
                return false;
            }

            return true;
        }

        protected static double ToSeconds(UInt64 val, UInt32 scale)
        {
            return ((double)val / (double)scale);
        }
    }

    public class SIDXAtom : AtomBase
    {
        public class SIDX_index_entry
        {
            public UInt32 RawRefsize { get; }
            public UInt32 RawDuration { get; }

            public UInt32 SAPData { get; }

            public UInt64 Offset { get; }

            public SIDX_index_entry(UInt32 refsize, UInt32 duration, UInt32 sapdata,
                                        UInt64 offdata)
            {
                RawRefsize = refsize;
                RawDuration = duration;
                SAPData = sapdata;
                Offset = offdata;
            }
        }

        public class Movie_index_entry : SIDX_index_entry
        {

            public TimeSpan SegmentDuration { get; }
            public TimeSpan TimeIndex { get; }
            public uint ID { get; }

            public Movie_index_entry(UInt32 raw_refsize, UInt32 raw_duration, UInt32 sap,
                                        UInt64 offdata, TimeSpan segment_duration, TimeSpan time_index, uint id)
                                        : base(raw_refsize, raw_duration, sap, offdata)
            {
                SegmentDuration = segment_duration;
                TimeIndex = time_index;
                ID = id;
            }
        }

        // Movieidx contains a list of content index entries
        // from single SIDX Atom.
        private List<Movie_index_entry> Movieidx { get; }
        private List<SIDX_index_entry> Sidxidx { get; }

        public uint MovieIndexCount => (uint)Movieidx.Count;

        public uint SIDXIndexCount => (uint)Sidxidx.Count;

        protected static byte[] AtomName = { (byte)'s', (byte)'i', (byte)'d', (byte)'x' };
        protected byte Version;
        protected byte[] Flags = new byte[3];
        public UInt32 ReferenceID { get; set; }
        public UInt32 Timescale { get; set; }
        public UInt64 RawPts { get; set; }
        public UInt64 RawOffset { get; set; }
        public TimeSpan AverageSegmentDuration { get; set; }

        public TimeSpan MaxIndexTime { get; set; }



        protected UInt16 Reserved;

        public SIDXAtom()
        {
            Movieidx = new List<Movie_index_entry>();
            Sidxidx = new List<SIDX_index_entry>();
        }

        public SIDXAtom(byte[] adata, ulong dataStart)
        {

            Movieidx = new List<Movie_index_entry>();
            Sidxidx = new List<SIDX_index_entry>();

            ParseAtom(adata, dataStart);
        }

        public (UInt64, UInt64, TimeSpan, TimeSpan) GetRangeData(uint idx)
        {

            UInt64 rl = 0;
            UInt64 rh = 0;
            TimeSpan startTime = default(TimeSpan);
            TimeSpan duration = default(TimeSpan);

            if (idx < MovieIndexCount)
            {
                rl = Movieidx[(int)idx].Offset;
                rh = rl + Movieidx[(int)idx].RawRefsize;
                startTime = Movieidx[(int)idx].TimeIndex;
                duration = Movieidx[(int)idx].SegmentDuration;
            }


            return (rl, rh, startTime, duration);
        }

        public void DumpMovieIndex(TimeSpan curr = default(TimeSpan))
        {
            Logger.Debug($"SIDX DB dump {Movieidx.Count} entries:");
            foreach (Movie_index_entry mie in Movieidx)
            {
                Logger.Debug(
                    $"Requested Time={curr} Index Start Time={mie.TimeIndex} Index Duration={mie.SegmentDuration} Total={mie.TimeIndex + mie.SegmentDuration}");
            }
        }

        public override void ParseAtom(byte[] adata, ulong dataStart)
        {
            int idx = 0;

            AtomSize = Read<UInt32>(adata, ref idx);

            //Sanity Check
            if (AtomSize > adata.Length)
                throw new InvalidDataException("SIDX buffer shorter then indicated by atom size.");


            // Check signature
            if (CheckName(adata, ref idx, AtomName) == false)
            {
                throw new InvalidDataException("Missing SIDX atom header.");
            }

            Version = Read<byte>(adata, ref idx);

            //Flags are only 3 bytes. Do byte at a time
            //as read will swap bytes around...
            Flags[0] = Read<byte>(adata, ref idx);
            Flags[1] = Read<byte>(adata, ref idx);
            Flags[2] = Read<byte>(adata, ref idx);

            ReferenceID = Read<UInt32>(adata, ref idx);

            Timescale = Read<UInt32>(adata, ref idx);

            UInt64 pts = 0;
            UInt64 offset = dataStart;

            if (Version == 0)
            {
                RawPts = Read<UInt32>(adata, ref idx);
                RawOffset = Read<UInt32>(adata, ref idx);
            }
            else
            {
                RawPts = Read<UInt64>(adata, ref idx);
                RawOffset = Read<UInt64>(adata, ref idx);
            }

            pts += RawPts;
            offset += RawOffset;

            Reserved = Read<UInt16>(adata, ref idx);

            UInt16 reference_count = Read<UInt16>(adata, ref idx);

            double avgSegDur = 0.0;
            int i = 1;
            uint indexCount = 0;

            Movieidx.Clear();
            Sidxidx.Clear();

            while (reference_count-- > 0)
            {

                UInt32 ref_size = Read<UInt32>(adata, ref idx);

                //C#, Why U no cast?!
                bool typeset = ((ref_size & 0x80000000) > 0) ? true : false;

                ref_size &= 0x7FFFFFF;


                UInt32 sseg_duration = Read<UInt32>(adata, ref idx);
                UInt32 SAPdata = Read<UInt32>(adata, ref idx);

                double currentDuration = ToSeconds(sseg_duration, Timescale);

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
                        new SIDX_index_entry(ref_size - 1, sseg_duration, SAPdata, offset)
                                );

                }
                else
                {
                    Movieidx.Add(
                        new Movie_index_entry(ref_size - 1, sseg_duration, SAPdata, offset,
                                                TimeSpan.FromSeconds(currentDuration),
                                                TimeSpan.FromSeconds(ToSeconds(pts, Timescale)),
                                                indexCount++
                                             )
                                );
                }

                pts += sseg_duration;

                offset += ref_size;

                //Assign max time contained within this particular SIDX Atom.
                //This is the last entry in SIDX box + its duration
                MaxIndexTime = TimeSpan.FromSeconds(ToSeconds(pts, Timescale));


            }

            AverageSegmentDuration = TimeSpan.FromSeconds(avgSegDur);

        }
    }
}
