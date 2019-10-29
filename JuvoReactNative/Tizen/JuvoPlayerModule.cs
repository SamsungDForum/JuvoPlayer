using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using ReactNative;
using ReactNative.Bridge;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoLogger;
using ILogger = JuvoLogger.ILogger;
using ElmSharp;
using ReactNative.Modules.Core;
using Newtonsoft.Json.Linq;


namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private Timer playbackTimer;
        private SeekLogic seekLogic = null; // needs to be initialized in the constructor!
        private ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public readonly string Tag = "JuvoRN";
        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        EcoreEvent<EcoreKeyEventArgs> _keyUp;
        Window window = ReactProgram.RctWindow; //The main window of the application has to be transparent. 
        List<StreamDescription>[] allStreamsDescriptions = { null, null, null };
        public IPlayerService Player { get; private set; }
        private IDisposable seekCompletedSub;
        public JuvoPlayerModule(ReactContext reactContext)
            : base(reactContext)
        {
            seekLogic = new SeekLogic(this);
            seekCompletedSub = seekLogic.SeekCompleted().Subscribe(message =>
            {
                var param = new JObject();
                SendEvent("onSeekCompleted", param);
            });
        }
        private void InitializeJuvoPlayer()
        {
            Player = new PlayerServiceProxy(new PlayerServiceImpl(window));
            Player.StateChanged()
               .Subscribe(OnPlayerStateChanged, OnPlaybackCompleted);
            Player.PlaybackError()
                .Subscribe(message =>
                {
                    var param = new JObject();
                    param.Add("Message", message);
                    SendEvent("onPlaybackError", param);
                });
            Player.BufferingProgress()
                .Subscribe(UpdateBufferingProgress);
        }
        public override string Name
        {
            get
            {
                return "JuvoPlayer";
            }
        }
        private void SendEvent(string eventName, JObject parameters)
        {
            Context.GetJavaScriptModule<RCTDeviceEventEmitter>()
                .emit(eventName, parameters);
        }
        public override void Initialize()
        {
            Context.AddLifecycleEventListener(this);
            _keyDown = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyDown, EcoreKeyEventArgs.Create);
            _keyDown.On += (s, e) =>
            {
                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyDown", param);
            };
            _keyUp = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyUp, EcoreKeyEventArgs.Create);
            _keyUp.On += (s, e) =>
            {
                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyUp", param);
            };
        }
        private void OnPlayerStateChanged(PlayerState state)
        {
            string value = "Idle";
            int interval = 100;
            switch (state)
            {
                case PlayerState.Prepared:
                    Player.Start();
                    playbackTimer = new Timer(
                        callback: new TimerCallback(UpdatePlayTime),
                        state: seekLogic.CurrentPositionUI,
                        dueTime: 0,
                        period: interval);
                    value = "Prepared";
                    break;
                case PlayerState.Playing:
                    value = "Playing";
                    playbackTimer.Change(0, interval); //resume progress info update
                    break;
                case PlayerState.Paused:
                    value = "Paused";
                    playbackTimer.Change(Timeout.Infinite, Timeout.Infinite); //suspend progress info update
                    break;
                case PlayerState.Idle:
                    playbackTimer?.Dispose();
                    break;
            }
            var param = new JObject();
            param.Add("State", value);
            SendEvent("onPlayerStateChanged", param);
        }
        private void OnPlaybackCompleted()
        {
            var param = new JObject();
            param.Add("State", "Completed");
            SendEvent("onPlaybackCompleted", param);
        }
        public void OnDestroy()
        {
            Logger?.Info("Destroying JuvoPlayerModule...");
            seekCompletedSub.Dispose();
        }
        public void OnResume()
        {
        }
        public void OnSuspend()
        {
        }
        private void UpdateBufferingProgress(int percent)
        {
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", (int)percent);
            SendEvent("onUpdateBufferingProgress", param);
        }
        private void UpdatePlayTime(object timerState)
        {
            string txt = "";
            if (Player?.CurrentCueText != null)
            {
                txt = Player?.CurrentCueText;
            }
            var param = new JObject();
            param.Add("Total", (int)seekLogic.Duration.TotalMilliseconds);
            param.Add("Current", (int)seekLogic.CurrentPositionUI.TotalMilliseconds);
            param.Add("SubtiteText", txt);
            SendEvent("onUpdatePlayTime", param);
        }
        private void Play(string videoURI, List<DRMDescription> drmDataList, string streamingProtocol)
        {
            try
            {
                if (videoURI == null) return;
                if (Player?.State == PlayerState.Playing) return;
                InitializeJuvoPlayer();
                Player.SetSource(new ClipDefinition
                {
                    Type = streamingProtocol,
                    Url = videoURI,
                    Subtitles = new List<SubtitleInfo>(),
                    DRMDatas = drmDataList
                });
            }
            catch (Exception e)
            {
                Logger?.Error(Tag, e.Message);
            }
        }

        public void OnError(Exception error)
        {
            var param = new JObject();
            param.Add("Message", error.Message);
            SendEvent("onPlaybackError", param);
        }

        //////////////////JS methods//////////////////
        [ReactMethod]
        public void GetStreamsDescription(int StreamTypeIndex)
        {
            var index = (JuvoPlayer.Common.StreamType)StreamTypeIndex;
            if (index == JuvoPlayer.Common.StreamType.Subtitle)
            {
                this.allStreamsDescriptions[StreamTypeIndex] = new List<StreamDescription>
                {
                    new StreamDescription
                    {
                        Default = true,
                        Description = "off",
                        Id = 0,
                        StreamType = (StreamType)StreamTypeIndex
                    }
                };
                this.allStreamsDescriptions[StreamTypeIndex].AddRange(Player.GetStreamsDescription((StreamType)StreamTypeIndex));
            }
            else
            {
                this.allStreamsDescriptions[StreamTypeIndex] = Player.GetStreamsDescription((StreamType)StreamTypeIndex);
            }
            var param = new JObject();
            param.Add("Description", Newtonsoft.Json.JsonConvert.SerializeObject(this.allStreamsDescriptions[StreamTypeIndex]));
            param.Add("StreamTypeIndex", (int)StreamTypeIndex);
            SendEvent("onGotStreamsDescription", param);
        }
        [ReactMethod]
        public void SetStream(int SelectedIndex, int StreamTypeIndex)
        {
            if (this.allStreamsDescriptions[StreamTypeIndex] == null) return;

            if (SelectedIndex != -1)
            {
                var index = (JuvoPlayer.Common.StreamType)StreamTypeIndex;
                if (index == JuvoPlayer.Common.StreamType.Subtitle)
                {
                    if (SelectedIndex == 0)
                    {
                        Player.DeactivateStream(StreamType.Subtitle);
                        return;
                    }
                }
                var stream = (StreamDescription)this.allStreamsDescriptions[StreamTypeIndex][SelectedIndex];
                Player.ChangeActiveStream(stream);
            }
        }
        [ReactMethod]
        public void Log(string message)
        {
            Logger?.Info(message);
        }
        [ReactMethod]
        public void StartPlayback(string videoURI, string drmDatasJSON, string streamingProtocol)
        {
            try
            {
                var drmDataList = (drmDatasJSON != null) ? JuvoPlayer.Utils.JSONFileReader.DeserializeJsonText<List<DRMDescription>>(drmDatasJSON) : new List<DRMDescription>();
                Play(videoURI, drmDataList, streamingProtocol);
            }
            catch (Exception e)
            {
                Logger?.Error(Tag, "StartPlayback failed... " + e.Message);
            }
            finally
            {
                seekLogic.Reset();
            }
        }
        [ReactMethod]
        public void StopPlayback()
        {
            Player?.Stop();
            Player?.Dispose();
            Player = null;
            seekLogic.IsSeekAccumulationInProgress = false;
        }
        [ReactMethod]
        public void PauseResumePlayback()
        {
            switch (Player.State)
            {
                case JuvoPlayer.Common.PlayerState.Playing:
                    Player?.Pause();
                    break;
                case JuvoPlayer.Common.PlayerState.Paused:
                    Player?.Start();
                    break;
            }
        }
        [ReactMethod]
        public void Forward()
        {
            seekLogic.SeekForward();
        }
        [ReactMethod]
        public void Rewind()
        {
            seekLogic.SeekBackward();
        }


    }
}