using System;
using System.IO;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Demuxers.FFmpeg;
using JuvoPlayer.SharedBuffers;
using Tizen.Applications;

namespace JuvoPlayer.DataProviders.Dash
{
    public class DashDataProviderFactory : IDataProviderFactory
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        public IDataProvider Create(ClipDefinition clip)
        {
            Logger.Info("Create.");
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
                Logger.Error(
                    "Cannot find application executable path.");
            }

            var manifest = new DashManifest(clip.Url);
            var audioPipeline = CreateMediaPipeline(StreamType.Audio, libPath);
            audioPipeline.DisableAdaptiveStreaming = true;
            var videoPipeline = CreateMediaPipeline(StreamType.Video, libPath);

            return new DashDataProvider(manifest, audioPipeline, videoPipeline);
        }

        private static DashMediaPipeline CreateMediaPipeline(StreamType streamType, string libPath)
        {
            var sharedBuffer = new ChunksSharedBuffer();
            var throughputHistory = new ThroughputHistory();
            var dashClient = new DashClient(throughputHistory, sharedBuffer, streamType);
            var demuxer = new FFmpegDemuxer(libPath, sharedBuffer);

            return new DashMediaPipeline(dashClient, demuxer, throughputHistory, streamType);
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
