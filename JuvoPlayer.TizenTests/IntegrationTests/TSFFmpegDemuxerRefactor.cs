using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ElmSharp;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using JuvoPlayer.SharedBuffers;
using JuvoPlayer.TizenTests.Utils;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSFFmpegDemuxerRefactor
    {
        private static string BigBuckBunnyUrl =
            "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";

        [Test]
        public void TestDemux()
        {
            var buffer = new ChunksSharedBuffer();
            var demuxer = CreateFFmpegDemuxer(buffer);
            var subs = new CompositeDisposable();

            Assert.DoesNotThrowAsync(async () =>
            {
                using (demuxer)
                {
                    demuxer.StartForExternalSource(InitializationMode.Full);

                    subs.Add(demuxer.StreamConfigReady()
                        .Subscribe(config => { Tizen.Log.Info("UT", config.ToString()); }));
                    subs.Add(demuxer.PacketReady()
                        .Subscribe(packet => { Tizen.Log.Info("UT", $"{packet?.StreamType} {packet?.Pts}"); }));

                    long chunkSize = 1000;
                    long from = 0, to = chunkSize;
                    while (from < 10000)
                    {
                        var data = await DownloadContent(BigBuckBunnyUrl, from, to);
                        Tizen.Log.Info("UT", $"Downloaded {data?.Length}");
                        buffer.WriteData(data);
                        from += chunkSize;
                        to += chunkSize;
                    }
                }

                subs.Dispose();
            });
        }

//        [Test]
//        [Repeat(25)]
//        public void TestAVFormatContextWrapper()
//        {
//            var glue = new FFmpegGlue();
//            glue.Initialize(ResolveFFmpegLibDir());
//
//            using (var wrapper = new AVFormatContextWrapper())
//            {}
//        }
//
//        [Test]
//        [Repeat(25)]
//        public void TestAVIOContextWrapper()
//        {
//            var glue = new FFmpegGlue();
//            glue.Initialize(ResolveFFmpegLibDir());
//
//            using (var wrapper = new AVIOContextWrapper(128 * 1024, ReadPacket))
//            {}
//        }
//
//        [Test]
//        [Repeat(25)]
//        public void TestAVIOContextWrapperAndAVFormatContextWrapper()
//        {
//            var glue = new FFmpegGlue();
//            glue.Initialize(ResolveFFmpegLibDir());
//
//            using (var ioContext = new AVIOContextWrapper(128 * 1024, ReadPacket))
//            using (var formatContext = new AVFormatContextWrapper())
//            {
//                ioContext.Seekable = false;
//                ioContext.WriteFlag = false;
//
//                formatContext.ProbeSize = 128 * 1024;
//                formatContext.MaxAnalyzeDuration = TimeSpan.FromSeconds(10);
//                formatContext.AVIOContext = ioContext;
//            }
//        }

        private ArraySegment<byte>? ReadPacket(int size)
        {
            return new byte[size];
        }

        private static IDemuxer CreateFFmpegDemuxer(ISharedBuffer sharedBuffer = null)
        {
            return new FFmpegDemuxerRefactor(ResolveFFmpegLibDir(), sharedBuffer);
        }

        private static string ResolveFFmpegLibDir()
        {
            return Path.Combine(Paths.ApplicationPath, "lib");
        }

        private async Task<byte[]> DownloadContent(string url, long from, long to)
        {
            var size = to - from;

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Range = new RangeHeaderValue(from, to);

                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var bytes = new byte[size];
                    var bytesread = stream.Read(bytes, 0, bytes.Length);
                    stream.Close();
                    return bytes;
                }
            }
        }
    }
}