using System;
using System.Collections.Concurrent;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class BufferSizeEntry
    {
        public int Size;
        public TimeSpan ClockStart;
        public TimeSpan ClockEnd;

    }

    class StreamBufferSize
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private ConcurrentQueue<BufferSizeEntry> sizeData = new ConcurrentQueue<BufferSizeEntry>();
        private BufferSizeEntry current;
        private int bufferSize;
        public int GetSize => bufferSize;
        public StreamType StreamType { get; set; }

        public void Add(Packet packet)
        {
            if (current == null)
            {
                current = new BufferSizeEntry
                {
                    ClockStart = packet.Dts
                };
            }

            current.Size += packet.Storage.Length;

            if (packet.Dts-current.ClockStart > TimeSpan.FromSeconds(0.25))
            {
                current.ClockEnd = packet.Dts;
                var size = Interlocked.Add(ref bufferSize, current.Size);
                

                //logger.Info($"{StreamType}: Entry Added {current.Size} {current.ClockEnd } {size}");

                current = null;
            }
        }

        public void Remove(TimeSpan clock)
        {
            var size = 0;

            //logger.Info($"{StreamType}: Buffer Size {bufferSize} {clock}");
            for (;;)
            {
                var ok = sizeData.TryPeek(out var entry);
                if (!ok)
                    break;

                //logger.Info($"{StreamType}: Entry {entry.ClockEnd} {clock}");
                if (entry.ClockEnd > clock)
                    break;

                ok = sizeData.TryDequeue(out entry);
                if (!ok)
                    break;

                size = Interlocked.Add(ref bufferSize, -entry.Size);
                //logger.Info($"{StreamType}: Removed {entry.ClockEnd} {clock}");
            }

            //logger.Info($"{StreamType}: Buffer Size {size}");
        }

        public void Clear()
        {
            logger.Info("");
            sizeData = new ConcurrentQueue<BufferSizeEntry>();
            Interlocked.Exchange(ref bufferSize, 0);
        }


    }
}
