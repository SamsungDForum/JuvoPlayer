/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Threading.Channels;

namespace JuvoLogger.Udp
{
    internal class ObjectPool<T> : IDisposable
    {
        public delegate T ObjectBuilder();
        public delegate void ObjectDisposer(T obj);

        private readonly Channel<T> _pool;
        private readonly ObjectBuilder Builder;
        private readonly ObjectDisposer Disposer;

        public ObjectPool(in int capacity, in ObjectBuilder builder, in bool singleReader = false, in bool singleWriter = false, in bool syncContinuations = false)
        : this(capacity, builder, null, singleReader, singleWriter, syncContinuations)
        {
        }

        public ObjectPool(in int capacity, in ObjectBuilder builder, in ObjectDisposer disposer, in bool singleReader = false, in bool singleWriter = false, in bool syncContinuations = false)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder), "No object builder provided (is null)");

            Builder = builder;
            Disposer = disposer == null && typeof(IDisposable).IsAssignableFrom(typeof(T))
                ? DefaultDisposer : disposer;

            _pool = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = syncContinuations,
                SingleReader = singleReader,
                SingleWriter = singleWriter
            });

            // Prefill pool with desired number of objects if needed.
        }

        public T Take()
        {
            return _pool.Reader.TryRead(out T obj) ? obj : Builder();
        }

        public void Return(T obj)
        {
            if (!_pool.Writer.TryWrite(obj))
                Disposer?.Invoke(obj);
        }
        private static void DefaultDisposer(T obj) => obj.Dispose();

        public void Dispose()
        {
            _pool.Writer.Complete();
            var reader = _pool.Reader;

            while (reader.TryRead(out T obj))
                Disposer?.Invoke(obj);
        }
    }
}
