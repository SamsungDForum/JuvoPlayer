using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using MpdParser;
using MpdParser.Node;

namespace JuvoPlayer.Dash
{

    internal class DashClient : IDashClient
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private static readonly TimeSpan MagicBufferTime = TimeSpan.FromSeconds(10);

        private readonly ISharedBuffer sharedBuffer;
        private readonly StreamType streamType;
        private readonly ManualResetEvent timeUpdatedEvent = new ManualResetEvent(false);

        private Media media;
        private TimeSpan currentTime;
        private uint currentSegmentId;

        private bool playback;
        private IRepresentationStream currentStreams;

        public DashClient(ISharedBuffer sharedBuffer, StreamType streamType)
        {
            this.sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;
            
        }

        public void Seek(TimeSpan position)
        {
            var segmentId = currentStreams?.MediaSegmentAtTime(position);
            if (segmentId != null)
                currentSegmentId = segmentId.Value;
        }

        public void Start()
        {
            if (media == null)
                throw new Exception("media has not been set");

            Logger.Info(string.Format("{0} DashClient start.", streamType));
            playback = true;

            Logger.Info(string.Format("{0} Media: {1}", streamType, media));
            // get first element of sorted array 
            var representation = media.Representations.OrderByDescending(o => o.Bandwidth).First();
            Logger.Info(representation.ToString());
            currentStreams = representation.Segments;

            Task.Run(() => DownloadThread()); 
        }

        public void Stop()
        {
            playback = false;
            timeUpdatedEvent.Set();

            sharedBuffer?.WriteData(null, true);
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
            try
            {
                timeUpdatedEvent.Set();

            }
            catch (Exception e)
            {
                // ignored
            }
        }

        private void DownloadThread()
        {
            try
            {
                DownloadInitSegment(currentStreams);
            }
            catch(Exception ex)
            {
                Logger.Error(string.Format("{0} Cannot download init segment file. Error: {1} {2}", streamType, ex.Message, ex.ToString()));
            }

            var bufferTime = TimeSpan.Zero;
            currentSegmentId = 0;
            while (true) 
            {
                while (bufferTime - currentTime > MagicBufferTime && playback)
                    timeUpdatedEvent.WaitOne();

                if (!playback)
                    return;

                try
                {
                    var stream = currentStreams.MediaSegmentAtPos(currentSegmentId);
                    if (stream != null)
                    {
                        var streamBytes = DownloadSegment(stream);

                        bufferTime += stream.Period.Duration;

                        sharedBuffer.WriteData(streamBytes);

                        if (!stream.EndSegment)
                        { 
                            ++currentSegmentId;
                            continue;
                        }
                    }

                    Stop();
                }
                catch (Exception ex)
                {
                    if (ex is WebException)
                    {
                        Logger.Error(string.Format("{0} Cannot download segment file. Error: {1} {2}", streamType, ex.Message, ex.ToString()));
                    }
                    else
                    {
                        Logger.Error(string.Format("Error: {0} {1} {2}", ex.Message, ex.TargetSite, ex.StackTrace));
                    }
                       
                }
            }
        }

        private byte[] DownloadSegment(MpdParser.Node.Dynamic.Segment stream)
        {
            Logger.Info(string.Format("{0} Downloading segment: {1} {2}", 
                streamType, stream.Url, stream.ByteRange));
            
            var url = stream.Url;

            var client = new WebClientEx();
            if (stream.ByteRange != null)
            {
                var range = new ByteRange(stream.ByteRange);
                client.SetRange(range.Low, range.High);
            }

            var streamBytes = client.DownloadData(url);

            Logger.Info(string.Format("{0} Segment downloaded.", streamType));

            return streamBytes;
        }

        private void DownloadInitSegment(
            IRepresentationStream streamSegments)
        {
            var initSegment = streamSegments.InitSegment;

            Logger.Info(string.Format("{0} Downloading Init segment: {1} {2}", 
                streamType, initSegment.ByteRange, initSegment.Url));

            var client = new WebClientEx();
            if (initSegment.ByteRange != null)
            {
                var range = new ByteRange(initSegment.ByteRange);
                client.SetRange(range.Low, range.High);
            }
            var streamBytes = client.DownloadData(initSegment.Url);
            sharedBuffer.WriteData(streamBytes);

            Logger.Info(string.Format("{0} Init segment downloaded.", streamType));
        }
    }
    internal class ByteRange
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
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
                Logger.Error(ex + " Cannot parse range.");
            }
        }
    }

    public class WebClientEx : WebClient
    {
        private long? from;
        private long? to;

        public void SetRange(long from, long to)
        {
            this.from = from;
            this.to = to;
        }

        public void ClearRange()
        {
            from = null;
            to = null;
        }

        public ulong GetBytes(Uri address)
        {
            OpenRead(address.ToString());
            return Convert.ToUInt64(ResponseHeaders["Content-Length"]);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            if (to != null && from != null)
            {
                request?.AddRange((int)from, (int)to);
            }
            return request;
        }
    }

}
