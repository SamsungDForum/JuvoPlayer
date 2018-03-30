using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Subtitles
{
    /// <summary>Maps a particular time offset to a corresponding cue.
    /// Implementation is based on Binary Search Symbol Table.</summary>
    internal class CuesMap
    {
        private List<Cue> cues = new List<Cue>();

        public int Count => cues.Count;

        /// <summary>Puts a given <see cref="T:JuvoPlayer.Subtitles.Cue"></see> in a map.
        /// If a map doesn't contain a cue with a overlapping range (NewCue.Begin is not
        /// in range [OldQue.Begin, OldQue.End)), then a given cue in instered in a map.
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
    }
}
