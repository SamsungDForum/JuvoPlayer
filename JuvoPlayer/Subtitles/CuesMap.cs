using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Subtitles
{
    internal class CuesMap
    {
        private List<Cue> cues = new List<Cue>();

        public int Count => cues.Count;

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

        public Cue Get(TimeSpan key)
        {
            if (Count == 0)
                return null;
            int i = Rank(key);
            if (i < Count && cues[i].Compare(key) == 0)
                return cues[i];
            return null;
        }

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
