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

namespace JuvoPlayer.Common
{
    public enum VideoCodec
    {
        H264 = 1,
        Vc1 = 2,
        Mpeg2 = 3,
        Mpeg4 = 4,
        Theora = 5,
        Vp8 = 6,
        Vp9 = 7,
        H263 = 8,
        Wmv1 = 9,
        Wmv2 = 10,
        Wmv3 = 11,
        Indeo3 = 12,
        H265 = 13
    }

    public enum AudioCodec
    {
        Aac = 1,
        Mp3 = 2,
        Pcm = 3,
        Vorbis = 4,
        Flac = 5,
        AmrNb = 6,
        AmrWb = 7,
        PcmMulaw = 8,
        GsmMs = 9,
        PcmS16Be = 10,
        PcmS24Be = 11,
        Opus = 12,
        Eac3 = 13,
        Mp2 = 14,
        Dts = 15,
        Ac3 = 16,
        Wmav1 = 17,
        Wmav2 = 18
    }
}
