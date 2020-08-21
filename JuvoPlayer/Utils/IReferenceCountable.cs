/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Threading;

// Code snatched from:
// https://gist.github.com/ufcpp/1c7977f4f5f7856787f3f2b6a8b13c8e

namespace JuvoPlayer.Common.Utils.IReferenceCountableExtensions
{
    using IReferenceCountable;
    public static class ReferenceCountable
    {
        public static T Share<T>(this T obj)
            where T : IReferenceCountable
        {
            Interlocked.Increment(ref obj.Count);
            return obj;
        }

        public static void Release<T>(this T obj)
            where T : IReferenceCountable
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
    public interface IReferenceCountable : IDisposable
    {
        ref int Count { get; }
    }
}