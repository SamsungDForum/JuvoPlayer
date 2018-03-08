using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Drms
{
    public class DRMException : Exception
    {
        public DRMException()
        {
        }

        public DRMException(string message) : base(message)
        {
        }

        public DRMException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
