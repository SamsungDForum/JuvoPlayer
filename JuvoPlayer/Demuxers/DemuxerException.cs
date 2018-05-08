using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Demuxers
{
    public class DemuxerException : Exception
    {
        public DemuxerException()
        {
        }

        public DemuxerException(string message) : base(message)
        {
        }

        public DemuxerException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
