using System;
using System.Linq;

namespace JuvoPlayer.TizenTests.Utils
{
    public class RandomOperationGenerator : TestOperationGenerator
    {
        private readonly Random _random;

        public RandomOperationGenerator()
        {
            _random = new Random();
        }

        public TestOperation NextOperation()
        {
            var operations = AllOperations.GetAll().ToList();
            var type = operations[_random.Next(operations.Count)];
            return (TestOperation) type.GetConstructor(Type.EmptyTypes).Invoke(null);
        }
    }
}