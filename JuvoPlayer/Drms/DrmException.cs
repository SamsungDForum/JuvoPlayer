using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Drms
{
    public class DrmException : Exception
    {
        public DrmException()
        {
        }

        public DrmException(string message) : base(message)
        {
        }

        public DrmException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
