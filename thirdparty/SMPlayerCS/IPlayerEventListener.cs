using System;
using System.Runtime.InteropServices;

namespace CSPlayer
{
    public enum StreamType_Samsung
    {
        STREAM_TYPE_SAMSUNG_AUDIO = 0,
        STREAM_TYPE_SAMSUNG_VIDEO
    };

    public enum PlayerErrorType_Samsung                    //This contains error type which you need to check and process
    {
        PLAYERERROR_UNKNOWN = 0,
        PLAYERERROR_UNSUPPORTED_CONTAINER,
        PLAYERERROR_UNSUPPORTED_CODEC,
        PLAYERERROR_NETWORK,
        PLAYERERROR_INITFAILED
    };

    public enum PlayerMsgType_Samsung                    //This contains MSG type which you need to check and process
    {
        MESSAGE_UNKNOWN = 0,
        MESSAGE_INIT_COMPLETE,
        MESSAGE_SEEK_DONE,
        MESSAGE_SEEK_COMPLETED,
        MESSAGE_END_OF_STREAM,
        MESSAGE_UPDATE_SUBTITLE
    };


    public interface IPlayerEventListener
    {
        //OnEnoughData means that sm-player dont want you to send data with type streamType, please stop submit and wait for related OnNeedData()
        void OnEnoughData(StreamType_Samsung streamType);

        //OnNeedData means player need you to send data with tpye streamType, size
        void OnNeedData(StreamType_Samsung streamType, uint size);


        void OnSeekData(StreamType_Samsung streamType, System.UInt64 offset);                  //usigned long long I use System.Uint64

        //OnError means that there is error need to check, please refer to PlayerErrorType_Samsung and msg.
        //For example, PLAYERERROR_NETWORK means network error, APP should stop play operation and show pop such as 'network error' etc
        void OnError(PlayerErrorType_Samsung errorType, string msg);

        //OnMessage means there is msg need to check from player. for details please refer to SMplayerAdapter.cs 's OnMessage
        void OnMessage(PlayerMsgType_Samsung msgType);

        //Init done
        void OnInitComplete();

        void OnInitFailed();

        void OnEndOfStream();

        void OnSeekCompleted();

        //SeekDone event, then you can submit new es data after seek
        void OnSeekStartedBuffering();

        //current playback time
        void OnCurrentPosition(System.UInt32 lCurrTime);
    }
}
