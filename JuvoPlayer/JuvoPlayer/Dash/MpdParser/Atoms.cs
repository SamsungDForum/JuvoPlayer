// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
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

using System;
using System.Collections.Generic;

namespace MpdParser.Node.Atom
{
    public abstract class AtomBase
    {
        static protected string tag = "JuvoPlayer";

        protected UInt32 AtomSize;

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
                // fallthroghs are intentional here...
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
                    Tizen.Log.Info(tag, string.Format("{0} Unsupported read type.", res.GetType().ToString()));
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

        public uint MovieIndexCount
        {
            get { return (uint)Movieidx.Count; }
          
        }
        public uint SIDXIndexCount
        {
            get { return (uint)Sidxidx.Count; }
            
        }

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

       
        public (UInt64, UInt64, TimeSpan) GetRangeDuration(TimeSpan curr)
        {
            Movie_index_entry i = Movieidx.Find(x =>
                (x.TimeIndex >= curr && (curr < x.TimeIndex + x.SegmentDuration)));
            UInt64 rl;
            UInt64 rh;
            TimeSpan ts;

            if (i != null)
            {
                rl = i.Offset;
                rh = rl + i.RawRefsize;
                ts = i.SegmentDuration;
            }
            else
            {
                rl = 0;
                rh = 0;
                ts = default(TimeSpan);
            }

            return (rl, rh, ts);
        }
        

        public (UInt64, UInt64, TimeSpan, TimeSpan) GetRangeData(uint idx)
        {

            UInt64 rl=0;
            UInt64 rh=0;
            TimeSpan starttime = default(TimeSpan);
            TimeSpan duration = default(TimeSpan);

            if ( idx < MovieIndexCount)
            {
                rl = Movieidx[(int)idx].Offset;
                rh = Movieidx[(int)idx].RawRefsize;
                starttime = Movieidx[(int)idx].TimeIndex;
                duration = Movieidx[(int)idx].SegmentDuration;
            }
            
            
            return (rl, rh, starttime, duration);
        }

        public uint? GetRangeDurationIndex(TimeSpan curr)
        {
            Movie_index_entry i = Movieidx.Find(x =>
                (x.TimeIndex >= curr && (curr < x.TimeIndex + x.SegmentDuration)));
            

            if (i != null)
            {
                Tizen.Log.Debug(tag, string.Format("Index entry ID: {0} for time {1}",i.ID,curr.ToString("HH:mm:ss")));
                return i.ID;
            }

            Tizen.Log.Debug(tag, string.Format("Index entry for time: {0} not found", curr.ToString("HH:mm:ss")));
            return null;
        }

        public void DumpMovieIndex(TimeSpan curr = default(TimeSpan))
        {
            Tizen.Log.Debug(tag, string.Format("SIDX DB dump {0} entries:", Movieidx.Count));
            foreach (Movie_index_entry mie in Movieidx)
            {
                Tizen.Log.Debug(tag, string.Format("Requested Time={0} Index Start Time={1} Index Duration={2} Total={3}",
                    curr, mie.TimeIndex, mie.SegmentDuration, mie.TimeIndex + mie.SegmentDuration));
            }
        }
        public override void ParseAtom(byte[] adata, ulong dataStart)
        {
            int idx = 0;

            AtomSize = Read<UInt32>(adata, ref idx);

            //Sanity Check
            if (AtomSize > adata.Length)
            {
                Tizen.Log.Info(tag, string.Format("SIDX buffer shorter then indicated by atom size."));
                return;
            }

            // Check signature
            if (CheckName(adata, ref idx, AtomName) == false)
            {
                Tizen.Log.Info(tag, string.Format("Missing SIDX atom header."));
                return;
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

            double AvgSegDur = 0.0;
            int i = 1;
            uint MovieIndexCount = 0;
            
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

                double currdurr = ToSeconds(sseg_duration, Timescale);
                double currttimeidx = ToSeconds(pts, Timescale);

                AvgSegDur = (currdurr - AvgSegDur) / i;
                i++;

                if (typeset)
                {
                    Sidxidx.Add(
                        new SIDX_index_entry(ref_size, sseg_duration, SAPdata, offset)
                                );
                   
                }
                else
                {
                    Movieidx.Add(
                        new Movie_index_entry(ref_size, sseg_duration, SAPdata, offset,
                                                TimeSpan.FromSeconds(currdurr),
                                                TimeSpan.FromSeconds(ToSeconds(pts, Timescale)),
                                                MovieIndexCount++
                                             )
                                );
                }

                pts += sseg_duration;
                //+1 We need to "point" to first byte of new data
                //as ref_size ammounts to # bytes that are to be read thus
                //# bytes + 1 is the next starting point
                offset += ref_size + 1;

                //Assign max time contained within this particular SIDX Atom.
                //This is the last entry in SIDX box + its duration
                MaxIndexTime = TimeSpan.FromSeconds(ToSeconds(pts, Timescale));


            }

            AverageSegmentDuration = TimeSpan.FromSeconds(AvgSegDur);

        }
    }
}