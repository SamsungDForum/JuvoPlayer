using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
//https://github.sec.samsung.net/VD8-PlusPlayer/plusplayer/commit/6a2085d96a8f941a701da6045d93033645ba7e8a#diff-8acc5c1e56c0afb3d9f70678f66a999a

namespace Tizen.TV.Multimedia.ESPlayer.Tests
{

    [TestFixture]
    [Description("Tizen.TV.Multimedia.ESPlayer Test")]
    class TSESPlayer
    {
        private static readonly string LogTag = "Tizen.Multimedia.ESPlayer";
        private static ElmSharp.Window window = null;

        [OneTimeSetUp]
        public static void OneTimeSetup()
        {
            Log.Info(LogTag, "start");
            window = ESPlayerTestHelper.CreateWindow();
            ESPlayerTestHelper.DownloadTestContents();
        }

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            Log.Info(LogTag, "start");
            ESPlayerTestHelper.DestroyWindow(window);
            window = null;
        }
        // ElmSharp : 추가지원 없음.
        // NUI : ... 사업부 기본 정책.
        // NUI : VD Platform 에서 개발하는 API. 
        // EFL C# : 센터에서 릴리즈. VD에서 사용은 안하고, public API. 안정적이지 않음. 
        // ELM C# : API 팀에서 만듬. 앞으로 미지원.
        [SetUp]
        public static void SetUp()
        {
            Log.Info(LogTag, "start");
        }

        [Test]
        public static void ESPlayer_Constructor_Test()
        {
            Log.Info(LogTag, "start");
            using (var player = new ESPlayer())
            {

            }
        }

        [Test]
        public static void ESPlayer_Create_Test()
        {
            Log.Info(LogTag, "start");

            Assert.DoesNotThrow(() =>
            {
                using (var player = new ESPlayer())
                {

                }
            });

            Log.Info(LogTag, "end");
        }

        [Test]
        public static void ESPlayer_Open_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_AV_Playback_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                ESPlayerTestHelper.WaitForEOS();

                Assert.DoesNotThrow(() => player.Stop());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_SetDisplayRoi_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                Assert.DoesNotThrow(() => player.SetDisplayMode(DisplayMode.DstRoi));
                Assert.DoesNotThrow(() => player.SetDisplayRoi(300, 300, 1280, 720));
                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                ESPlayerTestHelper.WaitForEOS();

                Assert.DoesNotThrow(() => player.Stop());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_SetDisplayVisible_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                await Task.Delay(5000);
                Assert.DoesNotThrow(() => player.SetDisplayVisible(false));

                await Task.Delay(5000);
                Assert.DoesNotThrow(() => player.SetDisplayVisible(true));


                ESPlayerTestHelper.WaitForEOS();

                Assert.DoesNotThrow(() => player.Stop());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_SetAudioMute_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                await Task.Delay(5000);
                Assert.DoesNotThrow(() => player.SetAudioMute(true));

                await Task.Delay(10000);
                Assert.DoesNotThrow(() => player.SetAudioMute(false));

                ESPlayerTestHelper.WaitForEOS();

                Assert.DoesNotThrow(() => player.Stop());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_Pause_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                await Task.Delay(3000);
                Assert.DoesNotThrow(() => player.Pause());
                Assert.AreEqual(player.GetState(), EsState.Paused);

                await Task.Delay(3000);
                Assert.DoesNotThrow(() => player.Resume());


                ESPlayerTestHelper.WaitForEOS();

                Assert.DoesNotThrow(() => player.Stop());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_Stop_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                await Task.Delay(5000);
                Assert.DoesNotThrow(() => player.Stop());
                Assert.AreEqual(player.GetState(), EsState.Idle);

                Assert.DoesNotThrow(() => player.Close());

            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_GetState_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                Assert.AreEqual(player.GetState(), EsState.None);

                Assert.DoesNotThrow(() => player.Open());
                Assert.AreEqual(player.GetState(), EsState.Idle);

                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);
                await expected;
                Assert.AreEqual(player.GetState(), EsState.Ready);

                Assert.IsNull(expected.Exception);

                player.Start();

                await Task.Delay(2000);

                Assert.AreEqual(player.GetState(), EsState.Playing);

                ESPlayerTestHelper.WaitForEOS();
                Assert.AreEqual(player.GetState(), EsState.Paused);

                Assert.DoesNotThrow(() => player.Stop());
                Assert.AreEqual(player.GetState(), EsState.Idle);

                Assert.DoesNotThrow(() => player.Close());
                Assert.AreEqual(player.GetState(), EsState.None);
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static async Task ESPlayer_GetPlayingTime_Test()
        {
            Log.Info(LogTag, "start");

            using (var player = new ESPlayer())
            {
                bool looping = true;
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));

                var expected = ESPlayerTestHelper.GetTaskForPrepareAsync(player);

                await expected;
                Assert.IsNull(expected.Exception);
                player.Start();

                var currentTimeTask = Task.Factory.StartNew(() =>
                {
                    while (looping)
                    {
                        var time = TimeSpan.FromMilliseconds(0);
                        Assert.DoesNotThrow(() => player.GetPlayingTime(out time));
                        Assert.AreNotEqual(time.TotalMilliseconds, TimeSpan.FromMilliseconds(0).TotalMilliseconds);

                        Log.Info(LogTag, $"time : {time}");

                        Thread.Sleep(500);
                    }
                });

                ESPlayerTestHelper.WaitForEOS();

                looping = false;
                await currentTimeTask;

                Assert.DoesNotThrow(() => player.Stop());
                Assert.DoesNotThrow(() => player.Close());
            }

            Log.Info(LogTag, "end");
        }

        [Test]
        public static void ESPlayer_SubmitHandlePacket_Test()
        {
            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetDisplay(window));
                Log.Info(LogTag, $"result : {player.SubmitPacket(new ESHandlePacket())}");
            }
        }

        [Test]
        public static async Task ESPlayer_Replace_StreamInfo_Test()
        {
            using (var player = new ESPlayer())
            {
                object wait = new object();

                player.EOSEmitted += (s, e) =>
                {
                    lock (wait)
                    {
                        Monitor.Pulse(wait);
                    }
                };

                player.Open();
                player.SetDisplay(window);

                var audioStream = ESPlayerTestHelper.GetAudioStreamInfo("audio_01");
                var videoStream = ESPlayerTestHelper.GetVideoStreamInfo("video_00");

                player.AddStream(audioStream.Value);
                player.AddStream(videoStream.Value);

                var task = player.PrepareAsync(async (stream) =>
                {
                    Log.Info(LogTag, $"ready to prepare callback. stream type : {stream}");

                    switch (stream)
                    {
                        case StreamType.Audio:
                            await ESPlayerTestHelper.PushPacketAsync(player, "audio_01", StreamType.Audio);
                            break;
                        case StreamType.Video:
                            await ESPlayerTestHelper.PushPacketAsync(player, "video_00", StreamType.Video);
                            break;
                    }
                });

                await task;

                player.Start();

                await Task.Delay(5000);

                player.Stop();

                Log.Info(LogTag, "Stop called.");

                ESPlayerTestHelper.looping = false;

                await Task.Delay(5000);

                ESPlayerTestHelper.looping = true;


                player.SetDisplay(window);
                audioStream = ESPlayerTestHelper.GetAudioStreamInfo("audio_01");
                videoStream = ESPlayerTestHelper.GetVideoStreamInfo("video_00");

                player.AddStream(audioStream.Value);
                player.AddStream(videoStream.Value);

                task = player.PrepareAsync(async (stream) =>
                {
                    Log.Info(LogTag, $"ready to prepare callback. stream type : {stream}");

                    switch (stream)
                    {
                        case StreamType.Audio:
                            await ESPlayerTestHelper.PushPacketAsync(player, "audio_01", StreamType.Audio);
                            break;
                        case StreamType.Video:
                            await ESPlayerTestHelper.PushPacketAsync(player, "video_00", StreamType.Video);
                            break;
                    }
                });

                await task;

                player.Start();

                lock (wait)
                {
                    Assert.True(Monitor.Wait(wait, 120 * 1000));
                }

                player.Stop();
                player.Close();
            }
        }

        [Test]
        public static void ESPlayer_UsingTrustZone_Test()
        {
            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());
                Assert.DoesNotThrow(() => player.SetTrustZoneUse(true));
                Assert.DoesNotThrow(() => player.SetTrustZoneUse(false));
            }
        }

        [Test]
        public static void ESPlayer_SetDrm_Test()
        {
            using (var player = new ESPlayer())
            {
                Assert.DoesNotThrow(() => player.Open());

                foreach (var type in new DrmType[] { DrmType.None, DrmType.Playready, DrmType.Verimatrix, DrmType.WidevineCdm }) {
                    Assert.DoesNotThrow(() => player.SetDrm(type));
                }
            }
        }

        /*
        [Test]
        public static async Task ESPlayer_PrepareAsync_Negative_Test()
        {
            using (var player = new ESPlayer())
            {
                var expected = await player.PrepareAsync();
                Assert.AreEqual(expected, true);
            }
        }

        [Test]
        public static async Task ESPlayer_Start_Test()
        {
            using (var player = new ESPlayer())
            {
                player.Create();
                player.Open(dummyUrl);
                var prepared = await player.PrepareAsync();
                var expected = player.Start();

                Thread.Sleep(500);

                Assert.AreEqual(prepared, true);
                Assert.AreEqual(expected, true);
            }
        }

        [Test]
        public static async Task ESPlayer_Seek_Test()
        {
            using (var player = new ESPlayer())
            {
                player.BufferStatusUpdated += (s, e) => {
                    switch(e.Status)
                    {
                        case BufferStatus.Overrun:
                            break;

                        case BufferStatus.Underrun:
                            player.SubmitPacket();

                            //
                            
                            break;
                    }
                };

                player.SeekAsync...Event += (s, e) =>
                {
                    // native seek 도중 buffer가 필요할 때.
                    Monitor.PulseAll(seekLocker);
                };

                player.Create();
                player.Open(dummyUrl);
                player.SetDisplay(window);
                await player.PrepareAsync();

                submitpacket();

                player.Start();

                //...


               var seek = player.SeekAsync(1000);


                

                // native seek이 완전히 끝나면 실행
            }
        }

        void SubmitPacketThread()
        {
            // thread
            lock(seekLocker)
            {
                Monitor.Wait(seekLocker);
                ESPlayer.submitPacket(packet);
            }
        }
        */
    }
}
