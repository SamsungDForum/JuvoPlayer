using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.TizenTests.Utils
{
    public class StateChangedTask
    {
        private readonly PlayerService _service;
        private readonly PlayerState _expectedState;
        private readonly CancellationToken _cancellationToken;
        private TimeSpan _timeout;

        public StateChangedTask(PlayerService service, PlayerState expectedState, CancellationToken token,
            TimeSpan timeout)
        {
            _service = service;
            _expectedState = expectedState;
            _cancellationToken = token;
            _timeout = timeout;
        }

        public Task Observe()
        {
            return Task.Run(async () =>
            {
                using (var timeoutCts = new CancellationTokenSource())
                using (var linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeoutCts.Token))
                {
                    timeoutCts.CancelAfter(_timeout);
                    while (_service.State != _expectedState && !linkedCts.IsCancellationRequested)
                    {
                        await Task.Delay(100, linkedCts.Token);
                    }
                }
            }, _cancellationToken);
        }

        public static Task Observe(PlayerService service, PlayerState expectedState, CancellationToken token,
            TimeSpan timeout)
        {
            return new StateChangedTask(service, expectedState, token, timeout).Observe();
        }
    }
}