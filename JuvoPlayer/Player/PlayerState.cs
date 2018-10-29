namespace JuvoPlayer.Player
{
    public enum PlayerState
    {
        Uninitialized,
        Ready,
        Buffering,
        Paused,
        Playing,
        Finished,
        Error = -1
    };
}