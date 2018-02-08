using System;
using System.Runtime.InteropServices;

namespace Tizen.TV.Smplayer
{
    public enum StreamType
    {
        Audio = 0,
        Video
    };

    public enum PlayerErrorType                    //This contains error type which you need to check and process
    {
        Unknown = 0,
        UnsupportedContainer,
        UnsupportedCodec,
        Network,
        InitFailed
    };

    public enum PlayerMsgType                    //This contains MSG type which you need to check and process
    {
        Unknown = 0,
        InitComplete,
        SeekDone,
        SeekCompleted,
        EndOfStream,
        UpdateSubtitle
    };

    public interface IPlayerEventListener
    {
        //OnEnoughData means that sm-player dont want you to send data with type streamType, please stop submit and wait for related OnNeedData()
        void OnEnoughData(StreamType streamType);

        //OnNeedData means player need you to send data with tpye streamType, size
        void OnNeedData(StreamType streamType, uint size);


        void OnSeekData(StreamType streamType, System.UInt64 offset);                  //usigned long long I use System.Uint64

        //OnError means that there is error need to check, please refer to PlayerErrorType and msg.
        //For example, Network means network error, APP should stop play operation and show pop such as 'network error' etc
        void OnError(PlayerErrorType errorType, string msg);

        //OnMessage means there is msg need to check from player. for details please refer to SMplayerAdapter.cs 's OnMessage
        void OnMessage(PlayerMsgType msgType);

        //Init done
        void OnInitComplete();

        void OnInitFailed();

        void OnEndOfStream();

        void OnSeekCompleted();

        //SeekDone event, then you can submit new es data after seek
        void OnSeekStartedBuffering();

        //current playback time
        void OnCurrentPosition(System.UInt32 currTime);
    }
}
