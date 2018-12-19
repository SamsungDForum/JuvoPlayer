using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Player.EsPlayer;

namespace JuvoPlayer.TizenTests.Utils
{
    public class SeekOperation : TestOperation
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        public async Task Execute(TestContext context)
        {
            var service = context.Service;
            var position = context.SeekTime ?? RandomSeekTime(service);

            _logger.Info($"Seeking to {position}");

            using (var timeoutCts = new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, context.Token))
            {
                timeoutCts.CancelAfter(context.Timeout);

                await service.SeekTo(position).WithCancellation(linkedCts.Token);

                for (var i = 0; i < 50; i++)
                {
                    var seekPos = position;
                    var curPos = service.CurrentPosition;
                    var diffMs = Math.Abs((curPos - seekPos).TotalMilliseconds);
                    if (diffMs < 500)
                        return;
                    await Task.Delay(200, linkedCts.Token);
                }

                throw new Exception("Seek failed");
            }
        }

        private TimeSpan RandomSeekTime(PlayerService service)
        {
            var rand = new Random();
            return TimeSpan.FromSeconds(rand.Next((int) service.Duration.TotalSeconds - 10));
        }
    }
}