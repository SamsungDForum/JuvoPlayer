using System;
using System.Collections.Generic;
using System.Text;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public class ErrorArgs : EventArgs
    {
        public ErrorType ErrorType
        {
            get; private set;
        }

        internal ErrorArgs(ErrorType errorType)
        {
            this.ErrorType = errorType;
        }
    }
}
