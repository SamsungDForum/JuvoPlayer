using JuvoPlayer.Common;
using JuvoPlayer.FFmpeg;
using System;
using System.IO;
using Tizen.Applications;

namespace JuvoPlayer.Dash
{
    public class DashDataProviderFactory : IDataProviderFactory
    {
        public IDataProvider Create(ClipDefinition clip)
        {
            Tizen.Log.Info("JuvoPlayer", "Create.");
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null.");
            }

            if (!SupportsClip(clip))
            {
                throw new ArgumentException("Unsupported clip type.");
            }

            string libPath = null;
            try
            {
                libPath = Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(
                        Application.Current.ApplicationInfo.ExecutablePath)),
                    "lib");
            }
            catch (NullReferenceException)
            {
                Tizen.Log.Error(
                    "JuvoPlayer",
                    "Cannot find application executable path.");
            }

            var manifest = new DashManifest(clip.Url);
            var audioPipeline = CreateMediaPipeline(StreamType.Audio, libPath);
            var videoPipeline = CreateMediaPipeline(StreamType.Video, libPath);

            return new DashDataProvider(manifest, audioPipeline, videoPipeline);
        }

        private static DashMediaPipeline CreateMediaPipeline(StreamType streamType, string libPath)
        {
            var sharedBuffer = new SharedBuffer();
            IDashClient dashClient = new DashClient(sharedBuffer, streamType);
            IDemuxer demuxer = new FFmpegDemuxer(sharedBuffer, libPath);

            return new DashMediaPipeline(dashClient, demuxer, streamType);
        }

        public bool SupportsClip(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null.");
            }

            return string.Equals(clip.Type, "Dash", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
