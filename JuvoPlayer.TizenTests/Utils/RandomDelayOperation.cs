using System;
using System.Threading.Tasks;

namespace JuvoPlayer.TizenTests.Utils
{
    public class RandomDelayOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var maxDelayTime = context.RandomMaxDelayTime;
            var rand = new Random();
            var next = rand.Next((int) maxDelayTime.TotalMilliseconds);
            return Task.Delay(TimeSpan.FromMilliseconds(next));
        }
    }
}