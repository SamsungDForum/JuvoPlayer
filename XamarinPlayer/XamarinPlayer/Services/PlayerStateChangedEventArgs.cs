using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XamarinMediaPlayer.Services
{
    public class PlayerStateChangedEventArgs : EventArgs
    {
        public PlayerStateChangedEventArgs(PlayerState state)
        {
            State = state;
        }
        public PlayerState State { get; }
    }
}
