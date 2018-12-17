using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.TizenTests.Utils
{
    public class SeekOperation : TestOperation
    {
        private ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        public Task Execute(TestContext context)
        {
            var service = context.Service;
            var position = context.SeekTime ?? RandomSeekTime(service);

            _logger.Info($"Seeking to {position}");
            service.SeekTo(position);

            return Task.Run(async () =>
            {
                using (var timeoutCts = new CancellationTokenSource())
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, context.Token))
                {
                    timeoutCts.CancelAfter(context.Timeout);
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        var seekPos = position;
                        var curPos = service.CurrentPosition;
                        var diffMs = Math.Abs((curPos - seekPos).TotalMilliseconds);
                        _logger.Info($"Current position {curPos}, diff Ms {diffMs}");
                        if (diffMs < 500)
                            return;
                        await Task.Delay(100, linkedCts.Token);
                    }
                }
            });
        }

        private TimeSpan RandomSeekTime(PlayerService service)
        {
            var rand = new Random();
            return TimeSpan.FromSeconds(rand.Next((int) service.Duration.TotalSeconds - 10));
        }
    }
}