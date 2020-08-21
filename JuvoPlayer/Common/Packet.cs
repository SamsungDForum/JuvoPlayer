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
using System.Xml.Serialization;

namespace JuvoPlayer.Common
{
    [Serializable]
    public class Packet : IDisposable
    {
        [XmlIgnore]
        public IDataStorage Storage { get; set; }

        public StreamType StreamType { get; set; }

        public TimeSpan Dts { get; set; }

        public TimeSpan Pts { get; set; }

        public bool IsKeyFrame { get; set; }

        public TimeSpan Duration { get; set; }

        public bool IsZeroClock()
        {
            return (Pts == TimeSpan.Zero && Dts == TimeSpan.Zero);
        }

        public static Packet operator -(Packet data, TimeSpan time)
        {
            data.Pts -= time;
            data.Dts -= time;
            return data;
        }

        public bool ContainsData()
        {
            return Storage != null;
        }

        public virtual void Prepend(byte[] prependData) =>
            Storage.Prepend(prependData);

        #region Disposable Support

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Storage?.Dispose();
        }

        #endregion
    }
}
