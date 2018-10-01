using System;
using System.Collections.Generic;
using System.Text;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public class ErrorEventArgs : EventArgs
    {
        public ErrorType ErrorType
        {
            get; private set;
        }

        internal ErrorEventArgs(ErrorType errorType)
        {
            this.ErrorType = errorType;
        }
    }
}
