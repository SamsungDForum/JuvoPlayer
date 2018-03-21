using System;
using System.Collections.Generic;
using System.Linq;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player
{
    class VideoCodecExtraDataHandler : ICodecExtraDataHandler
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly IPlayer player;
        private byte[] parsedExtraData = new byte[0];

        public VideoCodecExtraDataHandler(IPlayer player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");
        }

        public void OnAppendPacket(Packet packet)
        {
            if (packet == null)
                return;

            if (packet.StreamType != StreamType.Video)
                throw new ArgumentException("invalid packet type");

            if (parsedExtraData.Count() == 0)
                return;

            if (!packet.IsKeyFrame)
                return;

            var configPacket = new Packet()
            {
                Data = parsedExtraData.ToArray(),
                Dts = packet.Dts,
                Pts = packet.Pts,
                IsEOS = false,
                IsKeyFrame = true,
                StreamType = StreamType.Video
            };

            player.AppendPacket(configPacket);
        }

        public void OnStreamConfigChanged(StreamConfig config)
        {
            parsedExtraData = new byte[0];

            if (!(config is VideoStreamConfig))
                throw new ArgumentException("invalid config type");

            if (config.CodecExtraData == null)
                return;

            var videoConfig = (VideoStreamConfig)config;

            switch (videoConfig.Codec)
            {
                case VideoCodec.H264:
                    ExtractH264ExtraData(videoConfig);
                    break;
                case VideoCodec.H265:
                    ExtractH265ExtraData(videoConfig);
                    break;
                default:
                    break;
            }
        }
        private void ExtractH264ExtraData(VideoStreamConfig videoConfig)
        {
        }
        private void ExtractH265ExtraData(VideoStreamConfig videoConfig)
        {
        }

    }
}
