//
// Code snatched from:
// https://gist.github.com/ufcpp/1c7977f4f5f7856787f3f2b6a8b13c8e
//

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Runtime.CompilerServices
{
    internal class UsingBuilderAttribute : Attribute
    {
        public UsingBuilderAttribute(Type builderType) { BuilderType = builderType; }
        public Type BuilderType { get; }
    }
}

namespace JuvoPlayer.Common.Utils
{
    [UsingBuilder(typeof(ReferenceCoutableDisposer))]
    public interface IReferenceCoutable : IDisposable
    {
        ref int Count { get; }
    }

    public struct ReferenceCoutableDisposer
    {
        private IReferenceCoutable _r;
        public static ReferenceCoutableDisposer GetDisposer(IReferenceCoutable r) => new ReferenceCoutableDisposer(r);
        public ReferenceCoutableDisposer(IReferenceCoutable r) { _r = r; }
        public void Dispose() => _r.Release();
    }

    public static class ReferenceCoutable
    {
        public static T Init<T>(this T obj)
            where T : IReferenceCoutable
        {
            obj.Count = 1;
            return obj;
        }

        public static T Share<T>(this T obj)
            where T : IReferenceCoutable
        {
            Interlocked.Increment(ref obj.Count);
            return obj;
        }

        public static void Release<T>(this T obj)
            where T : IReferenceCoutable
        {
            if (Interlocked.Decrement(ref obj.Count) == 0)
            {
                obj.Dispose();
            }
        }
    }

    //
    // Test Code from original source file
    //

    /*
    unsafe struct Sample : IReferenceCoutable
    {
        private int* _pointer;

        public Sample(int x, int y)
        {
            _pointer = (int*)Marshal.AllocHGlobal(sizeof(int) * 3);
            _pointer[0] = x;
            _pointer[1] = y;
        }

        public ref int X => ref _pointer[0];
        public ref int Y => ref _pointer[1];
        ref int IReferenceCoutable.Count => ref _pointer[2];
        void IDisposable.Dispose() => Marshal.Release((IntPtr)_pointer);
    }


    class Program
    {
        static void Main(string[] args)
        {
            var items = new List<Sample>();

            using (var x = new Sample(1, 2).Init())
            //var x = new Sample(1, 2).Init(); try
            {
                for (int i = 0; i < 10; i++)
                {
                    items.Add(x.Share());
                }
            } // Call x.Release() instead of x.Dispose()
            //finally { x.Release(); }

            foreach (var item in items)
            {
                item.Release();
            }
        }
    }
    */
}