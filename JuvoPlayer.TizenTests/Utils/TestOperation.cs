using System.Threading.Tasks;

namespace JuvoPlayer.TizenTests.Utils
{
    public interface TestOperation
    {
        Task Execute(TestContext context);
    }
}