using System;

namespace XamarinPlayer.Services
{
    public class PlayerStateChangedEventArgs : EventArgs
    {
        public PlayerStateChangedEventArgs(PlayerState state)
        {
            State = state;
        }
        public PlayerStateChangedEventArgs(PlayerState state, string message)
        {
            State = state;
            Message = message;
        }
        public PlayerState State { get; }
        public string Message { get; }
    }
}
