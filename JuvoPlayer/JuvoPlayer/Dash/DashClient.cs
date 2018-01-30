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




        public DashClient(ISharedBuffer sharedBuffer, StreamType streamType)
        {
            this.sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;
            
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

        
        private void DownloadThread()
        {

            try
            {
                DownloadInitSegment(currentStreams);
            }
            catch(Exception ex)
            {
                Tizen.Log.Error(Tag, string.Format("{0} Cannot download init segment file. Error: {1}", streamType, ex.Message));
            }

            while (playback)
            {
                var currentTime = this.currentTime;
                while (bufferTime - currentTime <= magicBufferTime)
                {

                    try
                    {

                        var currentSegmentId = currentStreams.MediaSegmentAtTime(bufferTime);
                        var stream = currentStreams.MediaSegmentAtPos(currentSegmentId.Value);

                        byte[] streamBytes = DownloadSegment(stream);

                        bufferTime += stream.Period.Duration;
    
                       sharedBuffer.WriteData(streamBytes);
                    }
                    catch (Exception ex)
                    {
                        if (ex is WebException)
                        {
                            Tizen.Log.Error(Tag, string.Format("{0} Cannot download segment file. Error: {1} {2}", streamType, ex.Message, ex.ToString()));
                        }
                        else
                        {
                            Tizen.Log.Error(Tag, string.Format("Error: {0} {1} {2}", ex.Message, ex.TargetSite, ex.StackTrace));
                        }
                       
                    }
                }
            }
        }

        private byte[] DownloadSegment(MpdParser.Node.Dynamic.Segment stream)
        {
            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Downloading segment: {1} {2}", 
                streamType, stream.Url, stream.ByteRange));
            
            var url = stream.Url;

            var client = new WebClientEx();
            if (stream.ByteRange != null)
            {
                var range = new ByteRange(stream.ByteRange);
                client.SetRange(range.Low, range.High);
            }

            var streamBytes = client.DownloadData(url);

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Segment downloaded.", streamType));

            return streamBytes;
        }

        private void DownloadInitSegment(
            IRepresentationStream streamSegments)
        {
            var initSegment = streamSegments.InitSegment;

            Tizen.Log.Info("JuvoPlayer", string.Format("{0} Downloading Init segment: {1} {2}", 
                streamType, initSegment.ByteRange, initSegment.Url));

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

                if(Low > High )
                {
                    throw new ArgumentException("Range Low param cannot be higher then High param");
                }

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
