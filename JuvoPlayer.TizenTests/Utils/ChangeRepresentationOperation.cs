using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.TizenTests.Utils
{
    public class ChangeRepresentationOperation : TestOperation
    {
        public Task Execute(TestContext context)
        {
            var service = context.Service;
            var type = GetRandomStreamType();
            var description = GetRandomStreamDescription(service, type);
            if (description != null)
                service.ChangeActiveStream(description);
            return Task.CompletedTask;
        }

        private StreamType GetRandomStreamType()
        {
            var values = Enum.GetValues(typeof(StreamType));
            var random = new Random();
            return (StreamType) values.GetValue(random.Next(values.Length));
        }

        private StreamDescription GetRandomStreamDescription(PlayerService service, StreamType streamType)
        {
            var descriptions = service.GetStreamsDescription(streamType);
            if (descriptions.Count == 0)
                return null;

            var random = new Random();
            return descriptions[random.Next(descriptions.Count)];
        }
    }
}