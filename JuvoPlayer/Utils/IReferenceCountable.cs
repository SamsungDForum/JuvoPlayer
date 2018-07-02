//
// Code snatched from:
// https://gist.github.com/ufcpp/1c7977f4f5f7856787f3f2b6a8b13c8e
//
// Removed unsused code.
// Changed Init() to InitializeReferenceCounting()
//

using System;
using System.Threading;

namespace JuvoPlayer.Common.Utils.IReferenceCountableExtensions
{
    using JuvoPlayer.Common.Utils.IReferenceCountable;
    public static class ReferenceCoutable
    {
        public static T InitializeReferenceCounting<T>(this T obj)
            where T : IReferenceCoutable
        {
            obj.Count = 1;

            return obj;
        }

        public static T Share<T>(this T obj)
            where T : IReferenceCoutable
        {
            var r = Interlocked.Increment(ref obj.Count);
            return obj;
        }

        public static void Release<T>(this T obj)
            where T : IReferenceCoutable
        {
            var r = Interlocked.Decrement(ref obj.Count);
            if (r == 0)
            {
                obj.Dispose();
            }
        }
    }
}

namespace JuvoPlayer.Common.Utils.IReferenceCountable
{
    public interface IReferenceCoutable : IDisposable
    {
        ref int Count { get; }
    }
}