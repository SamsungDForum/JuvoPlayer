using System;

namespace JuvoPlayer.OpenGL.Services {
    public class PlayerStateChangedEventArgs : EventArgs
    {
        public PlayerStateChangedEventArgs(PlayerState state)
        {
            State = state;
        }
        public PlayerState State { get; }
    }
}
