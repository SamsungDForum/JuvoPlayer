/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
    public enum DrmInitDataType
    {
        Unknown,    // Unrecognized Init data type
        MsPrPro,    // Microsoft PlayReady PlayReady Header Object
        Pssh        // Pssh box, Demux or MPD Sourced. Content shall be same.
    }

    public class DrmInitData
    {
        public string[] KeyIDs;  // Key ID
        public DrmInitDataType DataType;
        public byte[] InitData = null;
        public byte[] SystemId = null;
        public StreamType StreamType { get; set; }

        public override int GetHashCode()
        {
            var hash = 2058005167 ^ InitData.Length;

            var len = InitData.Length;
            hash ^= len >= 4
                ? (InitData[0] << 12) | (InitData[1] << 8) | (InitData[2] << 4) | InitData[3]
                : hash;

            hash ^= len >= 8
                ? (InitData[len - 4] << 12) | (InitData[len - 3] << 8) | (InitData[len - 2] << 4) | InitData[len - 1]
                : hash;

            len = SystemId.Length;
            hash ^= len >= 4
                ? (SystemId[0] << 12) | (SystemId[1] << 8) | (SystemId[2] << 4) | SystemId[3]
                : hash;

            hash ^= len >= 8
                ? (SystemId[len - 4] << 12) | (SystemId[len - 3] << 8) | (SystemId[len - 2] << 4) | SystemId[len - 1]
                : hash;

            return hash.GetHashCode();
        }
    }
}
