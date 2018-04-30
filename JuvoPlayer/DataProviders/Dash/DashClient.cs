using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.SharedBuffers;
using MpdParser.Node;
using Representation = MpdParser.Representation;

namespace JuvoPlayer.DataProviders.Dash
{

    internal class DashClient : IDashClient
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private static readonly TimeSpan MagicBufferTime = TimeSpan.FromSeconds(10);
        private static readonly int MaxRetryCount = 3;

        private readonly ISharedBuffer sharedBuffer;
        private readonly StreamType streamType;
        private readonly AutoResetEvent timeUpdatedEvent = new AutoResetEvent(false);

        private Representation currentRepresentation;
        private TimeSpan currentTime;
        private uint currentSegmentId;

        private bool playback;
        private IRepresentationStream currentStreams;
        private byte[] initStreamBytes;
        private Task downloadTask;

        public DashClient(ISharedBuffer sharedBuffer, StreamType streamType)
        {
            this.sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;            
        }

        public TimeSpan Seek(TimeSpan position)
        {
            Logger.Info(string.Format("{0} Seek to: {1} ", streamType, position));

            var segmentId = currentStreams?.MediaSegmentAtTime(position);
            if (segmentId != null)
            {
                currentTime = position;
                currentSegmentId = segmentId.Value;

                return currentStreams.MediaSegmentAtPos(currentSegmentId).Period.Start;
            }

            return TimeSpan.Zero;
        }

        public void Start()
        {
            if (currentRepresentation == null)
                throw new Exception("currentRepresentation has not been set");

            Logger.Info(string.Format("{0} DashClient start.", streamType));
            playback = true;

            currentStreams = currentRepresentation.Segments;

            downloadTask = Task.Run(() => DownloadThread()); 
        }

        public void Stop()
        {
            StopPlayback();
            timeUpdatedEvent.Set();

            downloadTask.Wait();

            Logger.Info(string.Format("{0} Data downloader stopped", streamType));
        }

        private void StopPlayback()
        {
            // playback has been already stopped
            if (!playback)
                return;

            playback = false;
        }

        public void SetRepresentation(Representation representation)
        {
            // representation has changed, so reset initstreambytes
            if (currentRepresentation != null)
                initStreamBytes = null;

            currentRepresentation = representation;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;

            try
            {
                //this can throw when event is received after Dispose() was called
                timeUpdatedEvent.Set();
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        private void DownloadThread()
        {
            // clear garbage before appending new data
            sharedBuffer.ClearData();
            try
            {
                if (initStreamBytes == null)
                    initStreamBytes = DownloadInitSegment(currentStreams);

                if (initStreamBytes != null)
                    sharedBuffer.WriteData(initStreamBytes);
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("{0} Cannot download init segment file. Error: {1} {2}", streamType, ex.Message, ex.ToString()));
            }

            var duration = currentStreams.Duration;
            var downloadErrorCount = 0;
            var bufferTime = currentTime;
            while (true)
            {
                while (bufferTime - currentTime > MagicBufferTime && playback)
                    timeUpdatedEvent.WaitOne();

                if (!playback)
                {
                    SendEOSEvent();
                    return;
                }

                try
                {
                    var stream = currentStreams.MediaSegmentAtPos(currentSegmentId);
                    if (stream != null)
                    {
                        var streamBytes = DownloadSegment(stream);
                        downloadErrorCount = 0;

                        bufferTime += stream.Period.Duration;

                        sharedBuffer.WriteData(streamBytes);

                        if (bufferTime < duration)
                        { 
                            ++currentSegmentId;
                            continue;
                        }
                    }

                    StopPlayback();
                }
                catch (Exception ex)
                {
                    if (ex is WebException)
                    {
                        if (++downloadErrorCount >= MaxRetryCount)
                        {
                            Logger.Error(string.Format("{0} Cannot download segment file. Sending EOS event. Error: {1} {2}", streamType, ex.Message, ex.ToString()));
                            SendEOSEvent();
                            return;
                        }
                        Logger.Warn(string.Format("{0} Cannot download segment file. Will retry. Error: {1} {2}", streamType, ex.Message, ex.ToString()));
                    }
                    else
                    {
                        Logger.Error(string.Format("Error: {0} {1} {2}", ex.Message, ex.TargetSite, ex.StackTrace));
                    }  
                }
            }
        }

        private void SendEOSEvent()
        {
            sharedBuffer.WriteData(null, true);
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

        private byte[] DownloadInitSegment(
            IRepresentationStream streamSegments)
        {
            var initSegment = streamSegments.InitSegment;
            if (initSegment == null)
                return null;

            Logger.Info(string.Format("{0} Downloading Init segment: {1} {2}", 
                streamType, initSegment.ByteRange, initSegment.Url));

            var client = new WebClientEx();
            if (initSegment.ByteRange != null)
            {
                var range = new ByteRange(initSegment.ByteRange);
                client.SetRange(range.Low, range.High);
            }
            var bytes = client.DownloadData(initSegment.Url);

            Logger.Info(string.Format("{0} Init segment downloaded.", streamType));
            return bytes;
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
            var ranges = range.Split('-');
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
        private static readonly TimeSpan WebRequestTimeout = TimeSpan.FromSeconds(10);

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
            if (request != null)
            {
                request.Timeout = (int)WebRequestTimeout.TotalMilliseconds;
                if (to != null && from != null)
                {
                    request.AddRange((int)from, (int) to);
                }
            }
            return request;
        }
    }

}
