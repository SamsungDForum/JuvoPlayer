/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2019, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 */

using System;
using JuvoLogger;
using FFmpegBindings.Interop;

namespace JuvoPlayer.Common
{
    internal class FFmpegDataStorage : INativeDataStorage
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private static readonly byte[] AudioPES =
            {0xC0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE};

        private static readonly byte[] VideoPES =
            {0xE0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE};

        private int length = -1;

        internal StreamType StreamType { get; set; }

        public unsafe byte* Data => Packet.data;
        public int Length => GetLength();
        public void Prepend(byte[] prependData)
        {
            var prependLen = prependData.Length;
            var orgLen = Packet.size;
            var pkt = Packet;

            Span<byte> packetSpan;
            unsafe
            {
                if (FFmpeg.av_grow_packet(&pkt, prependLen) < 0)
                {
                    Logger.Error("GrowPacket failed");
                    return;
                }
                packetSpan = new Span<byte>(pkt.data, pkt.size);
            }

            // Regions overlap. Copy of source data will be made.
            packetSpan.Slice(0, orgLen).CopyTo(packetSpan.Slice(prependLen));
            prependData.AsSpan().CopyTo(packetSpan);

            Packet = pkt;
        }

        public AVPacket Packet { get; set; }

        private bool isDisposed;

        private unsafe int GetLength()
        {
            if (length > -1)
                return length;

            length = Packet.size;

            var removePes = Packet.side_data == null;
            if (removePes && IsPesPresent())
                length -= GetPes().Length;

            return length;
        }

        private unsafe bool IsPesPresent()
        {
            var suffixPes = GetPes();
            if (Packet.size < suffixPes.Length)
                return false;

            for (int i = 0, dataOffset = Packet.size - suffixPes.Length; i < suffixPes.Length; ++i)
                if (Packet.data[i + dataOffset] != suffixPes[i])
                    return false;

            return true;
        }

        private byte[] GetPes()
        {
            return StreamType == StreamType.Audio ? AudioPES : VideoPES;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            unsafe
            {
                var p = Packet;
                FFmpeg.av_packet_unref(&p);
            }

            isDisposed = true;
        }

        ~FFmpegDataStorage()
        {
            Dispose(false);
        }
    }
}