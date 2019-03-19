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

using JuvoPlayer.Drms;
using System;
using System.Runtime.ExceptionServices;
using System.Xml.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;

namespace JuvoPlayer.Drms
{
    [Serializable]
    public class EncryptedPacket : Packet
    {
        [Serializable]
        public struct Subsample
        {
            public uint ClearData;
            public uint EncData;
        }

        public byte[] KeyId;
        public byte[] Iv;
        public Subsample[] Subsamples;
        [XmlIgnore]
        public IDrmSession DrmSession;

        public async Task<Packet> Decrypt(CancellationToken token)
        {
            if (DrmSession == null)
                throw new InvalidOperationException("Decrypt called without DrmSession");

            return await DrmSession.DecryptPacket(this, token);
        }

        #region Disposable support
        // Use override to assure base class object references
        // of type EncryptedPacket will call this Dispose, not the base class
        //
        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
                DrmSession?.Release();

            IsDisposed = true;
        }
        #endregion
    }
}
