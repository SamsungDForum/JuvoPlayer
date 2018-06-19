using System;

namespace JuvoPlayer.OpenGL
{
    public class PlayerStateChangedEventArgs : EventArgs
    {
        public PlayerStateChangedEventArgs(PlayerState state)
        {
            State = state;
        }
       
        public PlayerState State { get; }
        
    }

    public class PlayerStateChangedStreamError : PlayerStateChangedEventArgs
    {
        public PlayerStateChangedStreamError(PlayerState state, string message)
            :base( state )
        {
            Message = message;
        }

        public string Message { get; }
    }
}
