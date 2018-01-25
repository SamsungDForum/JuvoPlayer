using JuvoPlayer.Common;
using MpdParser;
using MpdParser.Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace JuvoPlayer.Dash
{
    public abstract class AtomBase
    {
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
                    Tizen.Log.Info("AtomBase", string.Format("{0} Unsupported read type.", res.GetType().ToString()));
                    break;
            }



            return res;
        }

        protected static bool CheckName(byte[] adata, ref int idx, byte[] name)
        {
            if ( (adata.Length - idx )< 4)
            {
                return false;
            }

            // Check signature.   
            if (!(  name[0] == adata[idx++] &&
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

            public SIDX_index_entry(    UInt32 refsize, UInt32 duration, UInt32 sapdata, 
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

            public Movie_index_entry(   UInt32 raw_refsize, UInt32 raw_duration, UInt32 sap, 
                                        UInt64 offdata, TimeSpan segment_duration, TimeSpan time_index) 
                                        : base(raw_refsize, raw_duration, sap, offdata)
            {
                SegmentDuration = segment_duration;
                TimeIndex = time_index;
            }
        }

        
        public List<Movie_index_entry> Movieidx { get; }
        public List<SIDX_index_entry> Sidxidx { get; }

        protected static byte[] AtomName = { (byte)'s', (byte)'i', (byte)'d', (byte)'x' };
        protected byte Version;
        protected byte[] Flags = new byte[3];
        public UInt32 ReferenceID { get; set; }
        public UInt32 Timescale { get; set; }
        public UInt64 RawPts { get; set; }
        public UInt64 RawOffset { get; set; }
        public TimeSpan AverageSegmentDuration { get; set; }

        protected UInt16 Reserved;

        public SIDXAtom()
        {
            Movieidx = new List<Movie_index_entry>();
            Sidxidx = new List<SIDX_index_entry>();
        }

        public (UInt64, UInt64, TimeSpan ) GetRangeDuration(TimeSpan curr)
        {
            Movie_index_entry i = Movieidx.Find(x => 
                (x.TimeIndex >= curr && (curr < x.TimeIndex + x.SegmentDuration) ) );
            UInt64 rl;
            UInt64 rh;
            TimeSpan ts;

            if(i != null)
            {
                rl = i.Offset;
                rh = rl+i.RawRefsize;
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

        public void DumpMovieIndex(TimeSpan curr=default(TimeSpan) )
        {
            Tizen.Log.Debug("SIDXAtom", string.Format("SIDX DB dump {0} entries:", Movieidx.Count));
            foreach (Movie_index_entry mie in Movieidx)
            {
                Tizen.Log.Debug("SIDXAtom", string.Format("Requested Time={0} Index Start Time={1} Index Duration={2} Total={3}",
                    curr, mie.TimeIndex, mie.SegmentDuration, mie.TimeIndex+mie.SegmentDuration));
            }
        }
        public override void ParseAtom(byte[] adata, ulong dataStart)
        {
            int idx = 0;
            AtomSize = Read<UInt32>(adata, ref idx);

            //Sanity Check
            if (AtomSize > adata.Length)
            {
                Tizen.Log.Info("SIDXAtom", string.Format("SIDX buffer shorter then indicated atom size."));
                return;
            }

            // Check signature
            if (CheckName(adata, ref idx, AtomName) == false)
            {
                Tizen.Log.Info("SIDXAtom", string.Format("Missing SIDX atom header."));
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
                        new Movie_index_entry(  ref_size, sseg_duration, SAPdata, offset,
                                                TimeSpan.FromSeconds(currdurr),
                                                TimeSpan.FromSeconds( ToSeconds(pts, Timescale) ) 
                                             )
                                );
                       
                }

                pts += sseg_duration;
                //+1 We need to "point" to first byte of new data
                //as ref_size ammounts to # bytes that are to be read thus
                //# bytes + 1 is the next starting point
                offset += ref_size + 1; 
            }

            AverageSegmentDuration = TimeSpan.FromSeconds(AvgSegDur);

        }
    }

    internal class DashClient : IDashClient
    {
        private const string Tag = "JuvoPlayer";
        private static TimeSpan magicBufferTime = TimeSpan.FromSeconds(7);

        private ISharedBuffer sharedBuffer;
        private Media media;
        private StreamType streamType;

        private TimeSpan currentTime;
        private TimeSpan bufferTime;
        private bool playback;
        private IRepresentationStream currentStreams;

        // Instance of DashClient is per media type (Separate for A and V)
        // thus index info will be private per media type.
        private List<SIDXAtom> sidxs;


        public DashClient(ISharedBuffer sharedBuffer, StreamType streamType)
        {
            this.sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;
            sidxs = new List<SIDXAtom>();
        }

        public void Seek(TimeSpan position)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            if (media == null)
                throw new Exception("media has not been set");

            Tizen.Log.Info(Tag, string.Format("{0} DashClient start.", streamType));
            playback = true;

            Tizen.Log.Info(Tag, string.Format("{0} Media: {1}", streamType, media));
            // get first element of sorted array 
            var representation = media.Representations.First();
            Tizen.Log.Info(Tag, representation.ToString());
            currentStreams = representation.Segments;

            Task.Run(() => DownloadThread());
        }

        public void Stop()
        {
            playback = false;
        }

        public bool UpdateMedia(Media newMedia)
        {
            if (newMedia == null)
                return false;
            media = newMedia;
            return true;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;
        }

        private (UInt64, UInt64, TimeSpan)GetRangeDuration(TimeSpan curr)
        {
            UInt64 lb =0;
            UInt64 hb =0;
            TimeSpan durr = default(TimeSpan);

            foreach(SIDXAtom sidx in sidxs)
            {
                (lb, hb, durr) = sidx.GetRangeDuration(curr);
                if (lb != hb) break;
            }

            if(lb == hb )
            {
                Tizen.Log.Warn(Tag, string.Format("{0} Time Index {1} not found in indexing data", streamType, curr));
                foreach(SIDXAtom sidx in sidxs)
                {
                    sidx.DumpMovieIndex(curr);
                }

            }
            return (lb, hb, durr);
        }
        private void DownloadThread()
        {
            DownloadInitSegment(currentStreams);
            DownloadIndexSegment(currentStreams);
            
            while (playback)
            {
                var currentTime = this.currentTime;
                while (bufferTime - currentTime <= magicBufferTime)
                {
                    try
                    {

                        var currentSegmentId = currentStreams.MediaSegmentAtTime(bufferTime);
                        var stream = currentStreams.MediaSegmentAtPos(currentSegmentId.Value);

                        
                        UInt64 lb;
                        UInt64 hb;
                        TimeSpan ts;
                        

                        (lb, hb, ts) = GetRangeDuration(bufferTime);


                        byte[] streamBytes = DownloadSegment(stream,lb,hb);

                        if (lb != hb)
                        {
                            bufferTime += ts;
                        }
                        else
                        {
                            bufferTime += stream.Period.Duration;
                        }

                        // Chunk downloaded & timestamps updated so
                        // Check for end of stream condition (i.e. last chunk)
                        // 2 phase check to account to sub sec diffs.
                        // Stop playing if bufferTime >= stream Duration 
                        // OR
                        // if difference between the two is at sub second levels
                        bool eof = false;
                        if ((bufferTime >= stream.Period.Duration) ||
                            (Math.Abs((stream.Period.Duration - bufferTime).Ticks)) < TimeSpan.TicksPerSecond)
                        {
                            Tizen.Log.Info(Tag, string.Format("End of stream reached BuffTime {0} Duration {1}. Setting EOF flag", bufferTime, stream.Period.Duration));
                            eof = true;

                            // We are still looping after EOF so call it ourselves...
                            //otherwise some "do not play if EOF" reached in a loop would have to be done.
                            Stop();
                        }
                        Tizen.Log.Info(Tag, string.Format("BuffTime {0} Duration {1}", bufferTime, stream.Period.Duration));
                        sharedBuffer.WriteData(streamBytes, eof);
                    }
                    catch (Exception ex)
                    {
                        Tizen.Log.Error(Tag, string.Format("{0} Cannot download segment file. Error: {1}", streamType, ex.Message));
                    }
                }
            }
        }

        private byte[] DownloadSegment(MpdParser.Node.Dynamic.Segment stream, UInt64 lowB=0, UInt64 highB=0)
        {
            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Downloading segment: {1} {2}-{3}", 
                streamType, stream.Url, lowB, highB));
            
            var url = stream.Url;
            long startByte;
            long endByte;
            var client = new WebClientEx();
            if (stream.ByteRange != null)
            {
                var range = new ByteRange(stream.ByteRange);
                startByte = range.Low;
                endByte = range.High;
            }
            else
            {
                if (lowB != highB)
                {
                    startByte = (long)lowB;
                    endByte = (long)highB;
                }
                else
                {
                    startByte = 0;
                    endByte = (long)client.GetBytes(url);
                }
            }

            if (startByte != endByte)
            {
                client.SetRange(startByte, endByte);
            }
            else
            {
                client.ClearRange();
            }

            var streamBytes = client.DownloadData(url);

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Segment downloaded.", streamType));

            return streamBytes;
        }

        private void DownloadInitSegment(
            IRepresentationStream streamSegments)
        {
            var initSegment = streamSegments.InitSegment;

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Downloading segment: {1}", streamType, initSegment.Url));

            var client = new WebClientEx();
            if (initSegment.ByteRange != null)
            {
                var range = new ByteRange(initSegment.ByteRange);
                client.SetRange(range.Low, range.High);
            }
            var streamBytes = client.DownloadData(initSegment.Url);
            sharedBuffer.WriteData(streamBytes);

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Init segment downloaded.", streamType));
        }

        private void DownloadIndexSegment( IRepresentationStream streamSegments )
        {
            var indexSegment = streamSegments.IndexSegment;
            if( indexSegment == null )
            {
                Tizen.Log.Info("JuvoPlayer", string.Format("No SegmentBase indexRange for: {0}", streamType));
                return;
            }

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Downloading SegmentBase indexRange segment: {1}", streamType, indexSegment.Url));

            var client = new WebClientEx();
            ulong rh=0;
            if (indexSegment.ByteRange != null)
            {
                var range = new ByteRange(indexSegment.ByteRange);
                client.SetRange(range.Low, range.High);
                rh = (ulong)range.High;
            }
            var streamBytes = client.DownloadData(indexSegment.Url);
            SIDXAtom sidx = new SIDXAtom();
            sidx.ParseAtom(streamBytes, rh+1);

            sidxs.Add(sidx);

            //TODO:
            //SIDXAtom.SIDX_index_entry should contain a list of other sidx atoms containing
            //with index information. They could be loaded by updating range info in current
            //streamSegment and recursively calling DownloadIndexSegment - but about that we can worry later...
            //TO REMEMBER:
            //List of final sidxs should be "sorted" from low to high. In case of one it is not an issue,
            //it may be in case of N index boxes in hierarchy order (daisy chain should be ok too I think...)
            //so just do sanity check if we have such chunks
            if(sidx.Sidxidx.Count > 0 )
            {
                throw new NotImplementedException("Daisy chained / Hierarchical chunks not implemented...");
            }
            

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} SegmentBase indexRange downloaded.", streamType));

        }
    }
    internal class ByteRange
    {
        public long Low { get; }
        public long High { get; }
        public ByteRange(string range)
        {
            Low = 0;
            High = 0;
            var ranges = range.Split("-");
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Range cannot be parsed.");
            }
            try
            {
                Low = long.Parse(ranges[0]);
                High = long.Parse(ranges[1]);
            }
            catch (Exception ex)
            {
                Tizen.Log.Error("JuvoPlayer", ex + " Cannot parse range.");
            }
        }
    }

    public class WebClientEx : WebClient
    {
        private long? _from;
        private long? _to;

        public void SetRange(long from, long to)
        {
            _from = from;
            _to = to;
        }

        public void ClearRange()
        {
            _from = null;
            _to = null;
        }

        public ulong GetBytes(Uri address)
        {
            OpenRead(address.ToString());
            return Convert.ToUInt64(ResponseHeaders["Content-Length"]);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            if (_to != null && _from != null)
            {
                request?.AddRange((int)_from, (int)_to);
            }
            return request;
        }
    }

}
