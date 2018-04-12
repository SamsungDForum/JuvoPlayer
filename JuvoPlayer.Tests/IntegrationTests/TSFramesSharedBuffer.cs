using System;
using System.Collections.Generic;
using System.Text;
using JuvoPlayer.SharedBuffers;

namespace JuvoPlayer.Tests.IntegrationTests
{
    class TSFramesSharedBuffer : TSISharedBuffer
    {
        public override ISharedBuffer CreateSharedBuffer()
        {
            return new FramesSharedBuffer();
        }
    }
}
