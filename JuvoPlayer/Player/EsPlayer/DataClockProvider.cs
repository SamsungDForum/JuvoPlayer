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
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class DataClockProvider : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private TimeSpan _dataLimit = DataClockProviderConfig.TimeBufferDepthDefault;

        private readonly TimeSpan[] _streamDataLimits = new TimeSpan[(int)StreamType.Count];
        private TimeSpan _sourceClock;

        // Start / Stop may be called from multiple threads.
        private volatile IDisposable _dataClockConnection;

        private readonly IScheduler _scheduler;

        // Do not filter output to distinc values. Clients may start listening (without resubscription)
        // at their discretion.
        private readonly IConnectableObservable<TimeSpan> _dataClockSource;
        private readonly IObservable<TimeSpan> _dataClockObservable;
        private readonly Subject<TimeSpan> _dataClockSubject = new Subject<TimeSpan>();
        private readonly IDisposable _disabledClock = Disposable.Empty;

        private bool _isDisposed;

        public DataClockProvider(IScheduler scheduler)
        {
            _scheduler = scheduler;

            _dataClockSource =
                Observable.Interval(DataClockProviderConfig.ClockInterval, _scheduler)
                    .TakeWhile(_ => !_isDisposed)
                    .Select(GetDataClock)
                    .Multicast(_dataClockSubject);

            // Defer connection of IConnectObservable till very first subscription
            _dataClockObservable = Observable.Defer(StartOnSubscription);

            Logger.Info($"Initial Data Clock: {_sourceClock + _dataLimit}");
        }

        public IObservable<TimeSpan> DataClock()
        {
            return _dataClockObservable;
        }

        public async Task SetClock(TimeSpan newClock, CancellationToken token)
        {
            _dataClockConnection?.Dispose();

            await Observable.Start(() =>
            {
                _sourceClock = newClock;
                Logger.Info($"Clock set: {_sourceClock}");
            }, _scheduler);

            token.ThrowIfCancellationRequested();

            Start();
        }

        public async Task UpdateBufferDepth(StreamType stream, TimeSpan newDataLimit)
        {
            var isUpdated = await Observable.Start(() =>
            {
                _streamDataLimits[(int)stream] = newDataLimit;
                var currentHighest = _streamDataLimits.Max();
                if (_dataLimit == currentHighest)
                    return false;

                _dataLimit = currentHighest;
                return true;

            }, _scheduler);

            if (isUpdated)
                Logger.Info($"A/V Buffer depth set to {newDataLimit}");
        }

        private IObservable<TimeSpan> StartOnSubscription()
        {
            // Null identifies very fist subscription. 
            // internal enable/disabled is done with _disabledClock to differentiate
            // between first/connection, any subsequent connections.
            if (_dataClockConnection == null)
                _dataClockConnection = _dataClockSource.Connect();

            return _dataClockSource.AsObservable();
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
            var playerClock = PlayerClockProvider.LastClock;
            if (playerClock != PlayerClockProviderConfig.InvalidClock)
                _sourceClock = playerClock;

            return _sourceClock + _dataLimit;
        }

        public void Stop()
        {
            _dataClockConnection?.Dispose();
            _dataClockConnection = _disabledClock;
            _dataClockSubject.OnNext(PlayerClockProviderConfig.InvalidClock);
            Logger.Info("");
        }

        public void Start()
        {
            if (!ReferenceEquals(_dataClockConnection, _disabledClock)) return;

            _dataClockConnection = _dataClockSource.Connect();
            Logger.Info("");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _dataClockConnection?.Dispose();
            _dataClockSubject.Dispose();

            Logger.Info("");
        }
    }
}
