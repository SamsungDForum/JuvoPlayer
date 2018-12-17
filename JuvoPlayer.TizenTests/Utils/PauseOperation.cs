using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.TizenTests.Utils
{
    public class PauseOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var service = context.Service;
            service.Pause();
            return StateChangedTask.Observe(service, PlayerState.Paused, context.Token, context.Timeout);
        }
    }
}