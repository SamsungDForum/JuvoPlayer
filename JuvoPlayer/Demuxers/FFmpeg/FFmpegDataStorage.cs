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
using FFmpegBindings.Interop;
using JuvoLogger;
using JuvoPlayer.Common;
using ffmpeg = FFmpegBindings.Interop.FFmpeg;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    internal class FFmpegDataStorage : INativeDataStorage
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private static readonly byte[] AudioPes =
        {
            0xC0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE
        };

        private static readonly byte[] VideoPes =
        {
            0xE0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE
        };

        private bool _isDisposed;

        private int _length = -1;

        internal StreamType StreamType { get; set; }

        public AVPacket Packet { get; set; }

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
                if (ffmpeg.av_grow_packet(&pkt, prependLen) < 0)
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private unsafe int GetLength()
        {
            if (_length > -1)
                return _length;

            _length = Packet.size;

            var removePes = Packet.side_data == null;
            if (removePes && IsPesPresent())
                _length -= GetPes().Length;

            return _length;
        }

        private unsafe bool IsPesPresent()
        {
            var suffixPes = GetPes();
            if (Packet.size < suffixPes.Length)
                return false;

            for (int i = 0, dataOffset = Packet.size - suffixPes.Length; i < suffixPes.Length; ++i)
            {
                if (Packet.data[i + dataOffset] != suffixPes[i])
                    return false;
            }

            return true;
        }

        private byte[] GetPes()
        {
            return StreamType == StreamType.Audio ? AudioPes : VideoPes;
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            unsafe
            {
                var p = Packet;
                ffmpeg.av_packet_unref(&p);
            }

            _isDisposed = true;
        }

        ~FFmpegDataStorage()
        {
            Dispose(false);
        }
    }
}
