using System;
using System.Collections.Generic;
using System.Text;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public class BufferStatusArgs : EventArgs
    {
        public StreamType StreamType
        {
            get; private set;
        }

        public BufferStatus BufferStatus
        {
            get; private set;
        }

        internal BufferStatusArgs(StreamType streamType, BufferStatus bufferStatus)
        {
            this.StreamType = streamType;
            this.BufferStatus = bufferStatus;
        }
    }
}
