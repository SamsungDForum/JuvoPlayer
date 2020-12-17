/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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
 */

using System;
using FFmpegBindings.Interop;
using JuvoPlayer.Common;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public interface IAVFormatContext : IDisposable
    {
        long ProbeSize { get; set; }
        TimeSpan MaxAnalyzeDuration { get; set; }
        IAVIOContext AVIOContext { get; set; }
        TimeSpan Duration { get; }
        DrmInitData[] DRMInitData { get; }

        void Open();
        void Open(string url);
        void FindStreamInfo();
        int FindBestStream(AVMediaType mediaType);
        int FindBestBandwidthStream(AVMediaType mediaType);
        void EnableStreams(int audioIdx, int videoIdx);
        StreamConfig ReadConfig(int index);
        Packet NextPacket(int[] streamIndexes);
        void Seek(int stream, TimeSpan time);
    }
}