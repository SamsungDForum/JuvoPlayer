/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2019, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace JuvoPlayer.Tests.Utils
{
    public static class TaskExtensions
    {
        public static Task Never()
        {
            return Never<bool>();
        }

        public static Task<T> Never<T>()
        {
            var tcs = new TaskCompletionSource<T>();
            return tcs.Task;
        }

        public static async Task<T> WithCancellation<T>(this Task<T> nonCancellable, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (token.Register(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (nonCancellable != await Task.WhenAny(nonCancellable, tcs.Task).ConfigureAwait(false))
                    throw new OperationCanceledException(token.ToString());
            }

            return await nonCancellable;
        }

        public static async Task WithCancellation(this Task nonCancellable, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (token.Register(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (nonCancellable != await Task.WhenAny(nonCancellable, tcs.Task).ConfigureAwait(false))
                    throw new OperationCanceledException(token.ToString());
            }

            await nonCancellable;
        }

        public static async Task WithTimeout(this Task nonCancellable, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(timeout);
                await nonCancellable.WithCancellation(cts.Token);
            }
        }
    }
}