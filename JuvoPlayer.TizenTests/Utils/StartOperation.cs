using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.TizenTests.Utils
{
    public class StartOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var service = context.Service;
            service.Start();
            return StateChangedTask.Observe(service, PlayerState.Playing, context.Token, context.Timeout);
        }
    }
}