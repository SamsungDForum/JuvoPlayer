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
using System.Collections;
using System.Collections.Generic;

namespace JuvoPlayer.Util
{
    public class BinarySearchMap<T> : IEnumerable<T> where T : BinarySearchMap<T>.IItem
    {
        private readonly List<T> _items = new List<T>();

        public int Count => _items.Count;

        public T this[int index] => _items[index];

        /// <summary>
        ///     Returns an enumerator that iterates through the map.
        /// </summary>
        /// <returns>An enumerator that iterates through the map.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        /// <summary>
        ///     Returns an enumerator that iterates through the map.
        /// </summary>
        /// <returns>An enumerator that iterates through the map.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        ///     Puts a given item in a map.
        ///     If a map doesn't contain an item with a overlapping range (NewItem.Begin is not
        ///     in range [OldItem.Begin, OldItem.End)), then an given item in inserted in a map.
        ///     If a map contains an item with a overlapping range, then an given item replaces
        ///     an old item.
        /// </summary>
        /// <param name="item">An item to put in a map</param>
        public void Put(T item)
        {
            var rank = Rank(item.Begin);
            if (rank < Count && _items[rank].Compare(item.Begin) == 0)
            {
                _items[rank] = item;
                return;
            }

            _items.Insert(rank, item);
        }

        /// <summary>
        ///     Returns an item, which satisfies a condition:
        ///     <paramref name="key">key</paramref>
        ///     is in range of [item.Begin, item.End).
        /// </summary>
        /// <param name="key">A key used to lookup for item</param>
        /// <returns>Found item if a map contains it and null if not</returns>
        public T Get(TimeSpan key)
        {
            if (Count == 0)
                return default;
            var rank = Rank(key);
            if (rank < Count && _items[rank].Compare(key) == 0)
                return _items[rank];
            return default;
        }

        /// <summary>Returns a number of items smaller than a given <see cref="T:System.TimeSpan"></see> key.</summary>
        /// <param name="key">A key</param>
        /// <returns>Number of items smaller than a given key</returns>
        public int Rank(TimeSpan key)
        {
            return Rank(key, 0, _items.Count - 1);
        }

        private int Rank(TimeSpan key, int lo, int hi)
        {
            while (true)
            {
                if (hi < lo)
                    return lo;
                var mid = lo + (hi - lo) / 2;
                var cmp = _items[mid].Compare(key);
                if (cmp < 0)
                {
                    hi = mid - 1;
                    continue;
                }

                if (cmp <= 0)
                    return mid;
                lo = mid + 1;
            }
        }

        public interface IItem
        {
            TimeSpan Begin { get; }
            int Compare(TimeSpan pos);
        }
    }
}
