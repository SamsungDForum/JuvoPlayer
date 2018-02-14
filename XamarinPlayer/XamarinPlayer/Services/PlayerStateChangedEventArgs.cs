using System;

namespace XamarinPlayer.Services
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
