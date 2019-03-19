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

﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Subtitles
{
    /// <summary>Maps a particular time offset to a corresponding cue.
    /// Implementation is based on Binary Search Symbol Table.</summary>
    internal class CuesMap : IEnumerable<Cue>
    {
        private readonly List<Cue> cues = new List<Cue>();

        public int Count => cues.Count;

        /// <summary>Puts a given <see cref="T:JuvoPlayer.Subtitles.Cue"></see> in a map.
        /// If a map doesn't contain a cue with a overlapping range (NewCue.Begin is not
        /// in range [OldQue.Begin, OldQue.End)), then a given cue in inserted in a map.
        /// If a map contains a cue with a overlapping range, then a given cue replaces
        /// a old cue.</summary>
        /// <param name="cue">A cue to put in a map</param>
        public void Put(Cue cue)
        {
            int i = Rank(cue.Begin);
            if (i < Count && cues[i].Compare(cue.Begin) == 0)
            {
                cues[i] = cue;
                return;
            }
            cues.Insert(i, cue);
        }

        /// <summary>Returns a <see cref="T:JuvoPlayer.Subtitles.Cue"></see> cue,
        /// which satisfies a condition: <paramref name="value">value</paramref>
        /// is in range of [cue.Begin, cue.End).</summary>
        /// <param name="key">A key used to lookup for cue</param>
        /// <returns>Found <see cref="T:JuvoPlayer.Subtitles.Cue"></see> cue if a map contains it and null if not</returns>
        public Cue Get(TimeSpan key)
        {
            if (Count == 0)
                return null;
            int i = Rank(key);
            if (i < Count && cues[i].Compare(key) == 0)
                return cues[i];
            return null;
        }

        /// <summary>Returns a number of cues smaller than a given <see cref="T:System.TimeSpan"></see> key.</summary>
        /// <param name="key">A key</param>
        /// <returns>Number of cues smaller than a given key</returns>
        private int Rank(TimeSpan key)
        {
            return Rank(key, 0, cues.Count - 1);
        }

        private int Rank(TimeSpan key, int lo, int hi)
        {
            if (hi < lo) return lo;
            int mid = lo + (hi - lo) / 2;
            int cmp = cues[mid].Compare(key);
            if (cmp < 0)
                return Rank(key, lo, mid - 1);
            if (cmp > 0)
                return Rank(key, mid + 1, hi);
            return mid;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the Cues map.
        /// </summary>
        /// <returns>An enumerator that iterates through the Cues map.</returns>
        public IEnumerator<Cue> GetEnumerator()
        {
            return cues.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the Cues map.
        /// </summary>
        /// <returns>An enumerator that iterates through the Cues map.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
