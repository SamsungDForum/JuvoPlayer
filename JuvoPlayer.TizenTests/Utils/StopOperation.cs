using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.TizenTests.Utils
{
    public class StopOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var service = context.Service;
            service.Stop();
            return StateChangedTask.Observe(service, PlayerState.Idle, context.Token, context.Timeout);
        }
    }
}