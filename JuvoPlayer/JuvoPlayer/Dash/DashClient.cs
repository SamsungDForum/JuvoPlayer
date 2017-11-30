using System;
using System.Collections.Generic;
using System.Text;
using JuvoPlayer.Common;

namespace JuvoPlayer.Dash
{
    class DashClient : IDashClient
    {
        private SharedBufferByteArrayQueue sharedBuffer;

        public DashClient(SharedBufferByteArrayQueue sharedBuffer)
        {
            this.sharedBuffer = sharedBuffer;
        }

        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        public void Start(ClipDefinition clip)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
