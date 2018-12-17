using System.Threading.Tasks;

namespace JuvoPlayer.TizenTests.Utils
{
    public class DelayOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var delay = context.DelayTime;
            return Task.Delay(delay);
        }
    }
}