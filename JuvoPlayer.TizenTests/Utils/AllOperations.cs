using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JuvoPlayer.TizenTests.Utils
{
    public class AllOperations
    {
        public static IEnumerable<Type> GetAll()
        {
            var assembly = typeof(TestOperation).GetTypeInfo().Assembly;

            return from type in assembly.GetTypes()
                where typeof(TestOperation).IsAssignableFrom(type) && type != typeof(TestOperation)
                select type;
        }
    }
}