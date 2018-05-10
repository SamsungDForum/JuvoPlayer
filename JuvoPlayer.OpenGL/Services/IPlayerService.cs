using System;
using System.Collections.Generic;

namespace JuvoPlayer.OpenGL.Services
{
    public delegate void PlayerStateChangedEventHandler(object sender, PlayerStateChangedEventArgs e);

    public interface IPlayerService : IDisposable
    {
        event PlayerStateChangedEventHandler StateChanged;

        TimeSpan Duration { get; }

        TimeSpan CurrentPosition { get; }

        bool IsSeekingSupported { get; }

        PlayerState State { get; }

        string CurrentCueText { get; }

        void SetSource(object clip);

        void Start();

        void Stop();

        void Pause();

        void SeekTo(TimeSpan position);

        List<StreamDescription> GetStreamsDescription(StreamDescription.StreamType streamType);
        void ChangeActiveStream(StreamDescription stream);
        void DeactivateStream(StreamDescription.StreamType streamType);
    }
}