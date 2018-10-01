using System;
using System.Collections.Generic;
using System.Text;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public class BufferStatusEventArgs : EventArgs
    {
        public StreamType StreamType
        {
            get; private set;
        }

        public BufferStatus BufferStatus
        {
            get; private set;
        }

        internal BufferStatusEventArgs(StreamType streamType, BufferStatus bufferStatus)
        {
            this.StreamType = streamType;
            this.BufferStatus = bufferStatus;
        }
    }
}
