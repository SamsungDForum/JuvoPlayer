using System;

namespace Rtsp.Sdp
{
    public class EncriptionKey
    {
        public EncriptionKey(string p)
        {
        }

        public static EncriptionKey ParseInvariant(string value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            throw new NotImplementedException();
        }
    }
}
