/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;

namespace JuvoPlayer.Drms
{
    class CacheKeyComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null)
                return x == null && y == null;

            return x.Length == y.Length && x.SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            var len = obj.Length;
            var hash = 2058005163 ^ len;
            if (len == 0)
                return hash;

            // Use start/middle/end byte for hash value.
            hash ^= obj[0];
            hash ^= obj[len >> 1];
            hash ^= obj[len - 1];
            return hash;
        }
    }

    internal class DrmSessionCache
    {
        private readonly ConcurrentDictionary<byte[], IDrmSession> _cache =
            new ConcurrentDictionary<byte[], IDrmSession>(new CacheKeyComparer());

        public bool TryGetSession(IEnumerable<byte[]> keys, out IDrmSession session)
        {
            session = null;
            foreach (byte[] key in keys)
            {
                if (_cache.TryGetValue(key, out session))
                    return true;
            }

            return false;
        }

        public bool TryAddSession(IEnumerable<byte[]> keys, IDrmSession session)
        {
            var addCount = 0;
            addCount += keys.Count(key => _cache.TryAdd(key, session));

            // Duplicate keys are possible. Check if any were added.
            return addCount > 0;
        }

        public void Clear()
        {
            _cache.Values.AsParallel().ForAll(ses => ses.Release());
            _cache.Clear();
        }

        public override string ToString()
        {
            return _cache.Keys.Aggregate("Cached Sessions: " + _cache.Keys.Count,
                (current, key) => current + "\n" + DrmInitDataTools.KeyToUuid(key));
        }
    }
}
