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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Configuration;
using JuvoLogger;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class DataClockProvider : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private TimeSpan _dataLimit = DataClockProviderConfig.TimeBufferDepthDefault;
        private TimeSpan _sourceClock;

        // Start / Stop may be called from multiple threads.
        private volatile IDisposable _dataClockConnection;
        private readonly IScheduler _scheduler;

        // Do not filter output to distinct values. Clients may start listening (without re-subscription)
        // at their discretion.
        private readonly IConnectableObservable<TimeSpan> _dataClockSource;
        private readonly Subject<TimeSpan> _dataClockSubject = new Subject<TimeSpan>();
        private readonly PlayerClockProvider _playerClock;

        private bool _isDisposed;

        public DataClockProvider(IScheduler scheduler, PlayerClockProvider playerClock)
        {
            _scheduler = scheduler;
            _playerClock = playerClock;

            _dataClockSource =
                Observable.Interval(DataClockProviderConfig.ClockInterval, _scheduler)
                    .TakeWhile(_ => !_isDisposed)
                    .Select(GetDataClock)
                    .Multicast(_dataClockSubject);

            Logger.Info($"Initial Data Clock: {_sourceClock + _dataLimit}");
        }

        public IObservable<TimeSpan> DataClock()
        {
            return _dataClockSource.AsObservable();
        }

        public void SetClock(TimeSpan newClock, CancellationToken token)
        {
            Observable.Start(() =>
            {
                if (token.IsCancellationRequested)
                {
                    Logger.Info("SetClock cancelled");
                    return;
                }
                _sourceClock = newClock;
                Logger.Info($"Clock set: {_sourceClock + _dataLimit}");
            }, _scheduler);

        }

        public void UpdateBufferDepth(TimeSpan newDataLimit)
        {
            Observable.Start(() =>
            {
                _dataLimit = newDataLimit;
                Logger.Info($"A/V Buffer depth set to {newDataLimit}");

            }, _scheduler);
        }

        private TimeSpan GetDataClock(long i)
        {
            // NOTE:
            // DataClockProvider runs off player clock, emitting data requests
            // as player clock + data limit
            // Requirements:
            // - Player clock HAS TO start within time defined by DataLimit.
            //   If not, data providers will not be able to source new data.
            // - Live content. Player clock needs to start within first segment.
            // - Packet drop during seek (clock matching) cannot exceed  data limit
            //   If not, data providers will not be able to source new data.
            //
            // Data Limit   - Data limit is not enough to start player clock, consider 
            //                sourcing clock from raw packets. Do note, raw packet clocks may have 
            //                holes, old values, discontinuities or not yet seen fiendish imps.
            // Live Content - MPD based initial clock update may be required.
            // Packet drops - Notify data provided on dropped packets OR what is common clock
            //                so counting of buffered data can be done from that point.
            //
            var playerClock = _playerClock.LastClock;
            if (playerClock != PlayerClockProviderConfig.InvalidClock)
                _sourceClock = playerClock;

            return _sourceClock + _dataLimit;
        }

        public void Stop()
        {
            _dataClockConnection?.Dispose();
            _dataClockConnection = null;
            _dataClockSubject.OnNext(PlayerClockProviderConfig.InvalidClock);
            Logger.Info("");
        }

        public void Start()
        {
            if (_dataClockConnection != null) return;

            _dataClockConnection = _dataClockSource.Connect();
            Logger.Info($"Clock Started: {_sourceClock + _dataLimit}");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _dataClockConnection?.Dispose();
            _dataClockConnection = null;
            _dataClockSubject.Dispose();

            Logger.Info("");
        }
    }
}
