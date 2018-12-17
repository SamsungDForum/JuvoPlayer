using System.Threading.Tasks;
using JuvoPlayer.Common;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.Utils
{
    public class PrepareOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var service = context.Service;
            var clipTitle = context.ClipTitle;

            var clips = service.ReadClips();
            var clip = clips.Find(_ => _.Title.Equals(clipTitle));

            Assert.That(clip, Is.Not.Null);

            service.SetSource(clip);

            return StateChangedTask.Observe(service, PlayerState.Prepared, context.Token, context.Timeout);
        }
    }
}