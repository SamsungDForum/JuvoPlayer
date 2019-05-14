/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using System.Threading;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class SyncElement : IDisposable
    {
        private readonly ManualResetEventSlim syncWait = new ManualResetEventSlim(true);

        public bool IsSynchronized => syncWait.IsSet;

        protected void SetSyncState(bool newSyncState)
        {
            var hasChanged = IsSynchronized ^ newSyncState;

            if (!hasChanged)
                return;

            if (newSyncState)
            {
                syncWait.Set();
            }
            else
            {
                syncWait.Reset();
            }
        }

        public virtual void Reset()
        {
            syncWait.Set();
        }

        public void Wait(CancellationToken token)
        {
            syncWait.Wait(token);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                syncWait.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal class ClockSynchronizer : SyncElement
    {
        public TimeSpan Data { get; protected set; } = TimeSpan.Zero;
        public TimeSpan Reference { get; protected set; } = TimeSpan.Zero;

        protected TimeSpan HaltOnDifference;
        protected TimeSpan HaltOffDifference;

        public ClockSynchronizer(TimeSpan haltOn, TimeSpan haltOff)
        {
            SetThresholds(haltOn, haltOff);
        }

        public void SetThresholds(TimeSpan haltOn, TimeSpan haltOff)
        {
            HaltOnDifference = haltOn;
            HaltOffDifference = haltOff;

            // Thresholds changed. Unblock any waiters to allow new
            // limits to kick into place.
            base.Reset();
        }

        public void DataIn(TimeSpan dataClock)
        {
            Data = dataClock;

            if (dataClock - Reference >= HaltOnDifference)
                SetSyncState(false);
        }

        public void ReferenceIn(TimeSpan refClock)
        {
            Reference = refClock;
            if (Data - refClock <= HaltOffDifference)
                SetSyncState(true);
        }

        public override void Reset()
        {
            Data = TimeSpan.Zero;
            Reference = TimeSpan.Zero;
            base.Reset();
        }
    }
}
