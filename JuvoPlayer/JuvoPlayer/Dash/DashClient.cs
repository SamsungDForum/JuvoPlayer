using JuvoPlayer.Common;
using MpdParser;
using MpdParser.Node;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace JuvoPlayer.Dash
{
    internal class DashClient : IDashClient
    {
        private const string Tag = "JuvoPlayer";
        private static ISharedBuffer _sharedBuffer;
        private DashManifest _manifest;
        private static double _currentTime;
        private static double _bufferTime;
        private static bool _playback;
        private readonly Thread _dThread;
        private static IRepresentationStream _currentStreams;

        public DashClient(
            DashManifest dashManifest,
            ISharedBuffer sharedBuffer)
        {
            _manifest = dashManifest ??
                throw new ArgumentNullException(
                    nameof(dashManifest),
                    "dashManifest cannot be null");
            _sharedBuffer = sharedBuffer;
            if (_manifest.Document.Periods.Length == 0)
            {
                Tizen.Log.Error(Tag, "No periods present in MPD file.");
            }
            _dThread = new Thread(DownloadThread);
        }

        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            Tizen.Log.Info(Tag, "DashClient start.");
            _playback = true;
            foreach (var period in _manifest.Document.Periods)
            {
                Tizen.Log.Info(Tag, period.ToString());
                try
                {
                    var video = Find(period, "eng", MediaType.Video) ??
                        Find(period, "und", MediaType.Video);
                    var audio = Find(period, "eng", MediaType.Audio) ??
                        Find(period, "und", MediaType.Audio);

                    Tizen.Log.Info(Tag, "Media: " + video);
                    // get first element of sorted array 
                    var representation = video.Representations.First();
                    Tizen.Log.Info(Tag, representation.ToString());
                    _currentStreams = representation.Segments;
                    _dThread.Start();
                    Tizen.Log.Info(Tag, "data: " + _sharedBuffer.ReadData(1024));
                }
                catch (Exception ex)
                {
                    Tizen.Log.Info(Tag, ex.Message);
                }
            }
        }

        public void Stop()
        {
            _playback = false;
            _dThread.Abort();
        }

        public bool UpdateManifest(DashManifest newManifest)
        {
            if (newManifest == null) return false;
            _manifest = newManifest;
            return true;
        }

        public void OnTimeUpdated(double time)
        {
            _currentTime = time;
        }

        private static Media Find(
            MpdParser.Period p,
            string language,
            MediaType type,
            MediaRole role = MediaRole.Main)
        {
            Media missingRole = null;
            foreach (var set in p.Sets)
            {
                if (set.Type.Value != type || set.Lang != language) continue;
                if (set.HasRole(role))
                {
                    return set;
                }
                if (set.Roles.Length == 0)
                {
                    missingRole = set;

                }
            }
            return missingRole;
        }

        private static void DownloadThread()
        {
            DownloadInitSegment(_currentStreams);
            while (true)
            {
                var currentTime = _currentTime;
                var stream = _currentStreams.MediaSegmentAtPos((uint)currentTime);
                const double magicBufferTime = 7000.0; // miliseconds
                while (_playback &&
                       _bufferTime - currentTime <= magicBufferTime)
                {
                    try
                    {
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
                            startByte = 0;
                            endByte = (long)client.GetBytes(url);
                        }
                        _bufferTime += stream.Period.Duration.TotalMilliseconds;
                        if (startByte != endByte)
                        {
                            client.SetRange(startByte, endByte);
                        }
                        else
                        {
                            client.ClearRange();
                        }
                        var streamBytes = client.DownloadData(url);
                        _sharedBuffer.WriteData(streamBytes);
                    }
                    catch (Exception ex)
                    {
                        Tizen.Log.Error(
                            Tag,
                            "Cannot download segment file. Error: " + ex.Message);
                    }
                }
            }
        }

        private static void DownloadInitSegment(
            IRepresentationStream streamSegments)
        {
            var initSegment = streamSegments.InitSegment;
            var client = new WebClientEx();
            if (initSegment.ByteRange != null)
            {
                var range = new ByteRange(initSegment.ByteRange);
                client.SetRange(range.Low, range.High);
            }
            var streamBytes = client.DownloadData(initSegment.Url);
            _sharedBuffer.WriteData(streamBytes);
            Tizen.Log.Info("JuvoPlayer", "Init segment downloaded.");
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
