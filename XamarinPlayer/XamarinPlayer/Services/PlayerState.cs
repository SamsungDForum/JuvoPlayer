namespace XamarinPlayer.Services
{
    public enum PlayerState
    {
        Error = -1,
        Idle,
        Preparing,
        Prepared,
        Stopped,
        Playing,
        Paused,
        Completed,
        Buffering
    }
}
