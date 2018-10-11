using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Tizen.TV.Multimedia.ESPlayer.Tests
{
    internal static class ESPlayerTestHelper
    {
        private static readonly string LogTag = "Tizen.Multimedia.ESPlayer";

        private static readonly string serverPathPrefix = "http://168.219.241.200/vod/file/normal/video/es/welcome_movie/";
        private static readonly string tcFolderPrefix = "/tmp/esdata";
        private static readonly string tcESFileName = "ESP.es";
        private static readonly string tcCodecExtraDataFileName = "ESP.codec_extradata";
        private static readonly string tcStreamInfoFileName = "ESP.info";

        private static readonly object locker = new object();

        internal static void DownloadTestContents()
        {
            string[] folderList = { "audio_00", "audio_01", "video_00" };
            string[] fileList = { tcESFileName, tcCodecExtraDataFileName, tcStreamInfoFileName };

            foreach (var folder in folderList)
            {
                CreatePath(folder);

                foreach (var file in fileList)
                {
                    string path = serverPathPrefix + $"{folder}/{file}";
                    Log.Info(LogTag, $"path : {path}");

                    //$"http://10.88.105.104/WebAPITest/esdata/welcome_movie/{folder}/{file}";
                    string to = $"{tcFolderPrefix}/{folder}/{file}";
                    DownloadFileFrom(path, to);
                }
            }
        }

        internal static ElmSharp.Window CreateWindow()
        {
            var window = new ElmSharp.Window("ut-esplayer-csapi");
            window.Resize(1920, 1080);
            window.Realize(null);
            window.Active();
            window.Show();

            return window;
        }

        internal static void DestroyWindow(ElmSharp.Window window)
        {
            window.Hide();
            window.Unrealize();
        }

        internal static byte[] GetExtraCodecData(string path)
        {
            try
            {
                using (var reader = new BinaryReader(new FileStream(path, FileMode.Open)))
                {
                    var size = reader.ReadInt32();
                    var buffer = reader.ReadBytes(size);
                    Log.Info(LogTag, $"extra codec data size : {size}");
                    return buffer;
                }
            }
            catch (Exception ex)
            {
                Log.Error(LogTag, $"File I/O is failure. ex : {ex.Message}");
                return null;
            }
        }

        private static void CreatePath(string folderName)
        {
            var path = $"{tcFolderPrefix}/{folderName}";

            if(!Directory.Exists(tcFolderPrefix))
            {
                Directory.CreateDirectory(tcFolderPrefix);
            }

            if(!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Log.Info(LogTag, $"{path} is created.");
            }
            else
            {
                Log.Info(LogTag, $"{path} already exists.");
            }
        }

        private static void DownloadFileFrom(string path, string to)
        {
            try
            {
                var exists = File.Exists(to);
                Log.Info(LogTag, $"Does {to} exists ? : {exists}");

                if (exists)
                    return;

                using (var client = new WebClient())
                {
                    client.DownloadFile(path, to);
                }

            }
            catch (Exception ex)
            {
                Log.Error(LogTag, $"File download failed. ex : {ex.Message}");
            }
        }

        private static string GetStreamInfoFilePath(string folderName)
        {
            string path = $"{tcFolderPrefix}/{folderName}/{tcStreamInfoFileName}";
            Log.Info(LogTag, $"path : {path}");
            return path;
        }

        private static string GetCodecExtraDataFilePath(string folderName)
        {
            string path = $"{tcFolderPrefix}/{folderName}/{tcCodecExtraDataFileName}";
            Log.Info(LogTag, $"path : {path}");
            return path;
        }

        private static string GetESFilePath(string folderName)
        {
            string path = $"{tcFolderPrefix}/{folderName}/{tcESFileName}";
            Log.Info(LogTag, $"path : {path}");
            return path;
        }

        internal static AudioStreamInfo? GetAudioStreamInfo(string folderName)
        {
            try
            {
                /*
                audio/x-ac3
                48000
                2
                0
                */
                var infoPath = GetStreamInfoFilePath(folderName);
                var infosFromFile = File.ReadAllLines(infoPath);

                var extraCodecDataPath = GetCodecExtraDataFilePath(folderName);
                var extraCodecData = GetExtraCodecData(extraCodecDataPath);

                var info = new AudioStreamInfo
                {
                    codecData = extraCodecData,
                    mimeType = AudioMimeType.Ac3, //infosFromFile[0],
                    sampleRate = Convert.ToInt32(infosFromFile[1]),
                    channels = Convert.ToInt32(infosFromFile[2])
                    //info.version = Convert.ToInt32(infosFromFile[3]);
                };

                return info;
            }
            catch (Exception ex)
            {
                Log.Error(LogTag, $"File I/O is failure. ex : {ex.Message}");
                return null;
            }
        }

        internal static VideoStreamInfo? GetVideoStreamInfo(string folderName)
        {
            try
            {
                /*
                video/x-h265
                3840
                2160
                3840
                2160
                60
                1
                */
                var infoPath = GetStreamInfoFilePath(folderName);
                var infosFromFile = File.ReadAllLines(infoPath);

                var extraCodecDataPath = GetCodecExtraDataFilePath(folderName);
                var extraCodecData = GetExtraCodecData(extraCodecDataPath);

                var info = new VideoStreamInfo
                {
                    codecData = extraCodecData,
                    mimeType = VideoMimeType.Hevc,//infosFromFile[0],
                    width = Convert.ToInt32(infosFromFile[1]),
                    height = Convert.ToInt32(infosFromFile[2]),
                    maxWidth = Convert.ToInt32(infosFromFile[3]),
                    maxHeight = Convert.ToInt32(infosFromFile[4]),
                    num = Convert.ToInt32(infosFromFile[5]),
                    den = Convert.ToInt32(infosFromFile[6])
                };

                return info;
            }
            catch (Exception ex)
            {
                Log.Error(LogTag, $"File I/O is failure. ex : {ex.Message}");
                return null;
            }
        }

        private static bool ReadNextPacketAndPush(BinaryReader reader, StreamType streamType, ESPlayer player)
        {
            try
            {
                //if(player.GetState() < EsState.Ready)
                {
                    //Log.Error(LogTag, "ESPlayer state is not valid.");
                    //return false;
                }

                var packet = new ESPacket
                {
                    type = streamType,
                    pts = reader.ReadUInt64(),
                    duration = reader.ReadUInt64(),
                };

                var bufferSize = reader.ReadUInt64();

                packet.buffer = reader.ReadBytes((int)bufferSize);

                //Log.Info(LogTag, $"packet [type][pts][duration][bufferLength] : [{packet.type}][{packet.pts}][{packet.duration}][{packet.bufferSize}]");
                
                SubmitStatus status = SubmitStatus.NotPrepared;

                while (status != SubmitStatus.Success)
                {
                    status = player.SubmitPacket(packet);

                    if (!looping)
                    {
                        Log.Info(LogTag, $"{streamType} Break loop.");
                        break;
                    }
                }

                return true;
            }
            catch(Exception ex)
            {
                Log.Error(LogTag, $"End of {streamType} stream reached. ex : {ex.Message}");
                return false;
            }
        }

        internal static bool looping = true;

        internal static async Task PushPacketAsync(ESPlayer player, string folderName, StreamType streamType)
        {
            var task = Task.Factory.StartNew(() =>
            {
                try
                {
                    var path = GetESFilePath(folderName);
                    using (var reader = new BinaryReader(new FileStream(path, FileMode.Open)))
                    {
                        Log.Info(LogTag, $"Start push es packet from {path}");
                        while(looping)
                        {
                            var ret = ReadNextPacketAndPush(reader, streamType, player);

                            if(!ret)
                            {
                                Log.Info(LogTag, $"All es {streamType} packet is pushed to esplayer.");
                                player.SubmitEosPacket(streamType);

                                return;
                            }
                        }

                        Log.Info(LogTag, $"{streamType} push task loop break.");
                    }
                }
                catch(Exception ex)
                {
                    Log.Error(LogTag, $"ex : {ex.Message}");
                    Log.Error(LogTag, $"trace : {ex.StackTrace}");
                }
            });

            await task;
        }

        internal static Task GetTaskForPrepareAsync(ESPlayer player)
        {
            player.ErrorOccurred += (s, e) =>
            {
                Log.Error(LogTag, $"ErrorOccurred. error code : {e.ErrorType}");
            };

            player.EOSEmitted += (s, e) =>
            {
                Log.Info(LogTag, $"EOSEmitted.");
                PulseLockObject();
            };

            var audioStream = GetAudioStreamInfo("audio_01");
            var videoStream = GetVideoStreamInfo("video_00");

            Assert.IsNotNull(audioStream);
            Assert.IsNotNull(videoStream);

            Assert.DoesNotThrow(() => player.AddStream(audioStream.Value));
            Assert.DoesNotThrow(() => player.AddStream(videoStream.Value));

            return player.PrepareAsync(async (stream) =>
            {
                Log.Info(LogTag, $"ready to prepare callback. stream type : {stream}");

                switch (stream)
                {
                    case StreamType.Audio:
                        await PushPacketAsync(player, "audio_01", StreamType.Audio);
                        break;
                    case StreamType.Video:
                        await PushPacketAsync(player, "video_00", StreamType.Video);
                        break;
                }
            });
        }

        internal static void PulseLockObject()
        {
            lock (locker)
            {
                Monitor.Pulse(locker);
            }
        }

        internal static void WaitForEOS()
        {
            lock (locker)
            {
                Assert.True(Monitor.Wait(locker, 60 * 1000));
            }
        }
    }
}
