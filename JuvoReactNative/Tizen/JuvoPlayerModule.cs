using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading;
using ReactNative;
using ReactNative.Bridge;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoLogger;
using ILogger = JuvoLogger.ILogger;
using Log = Tizen.Log;
using ElmSharp;
using ReactNative.Modules.Core;
using Newtonsoft.Json.Linq;


namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private PlayerServiceProxy juvoPlayer = null;
        private readonly TimeSpan UpdatePlaybackInterval = TimeSpan.FromMilliseconds(100);
        private static Timer playbackTimer;
        private SeekLogic seekLogic = null; // needs to be initialized in the constructor!
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";
        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        EcoreEvent<EcoreKeyEventArgs> _keyUp;
        SynchronizationContext syncContext;
        Window window = ReactProgram.RctWindow; //The main window of the application has to be transparent.
        public TimeSpan CurrentPositionPlayer => juvoPlayer?.CurrentPosition ?? TimeSpan.Zero;
        public TimeSpan Duration => juvoPlayer?.Duration ?? TimeSpan.Zero;
        public JuvoPlayer.Common.PlayerState State => ((IPlayerService)juvoPlayer)?.State ?? JuvoPlayer.Common.PlayerState.Idle;
        public bool IsSeekingSupported => juvoPlayer?.IsSeekingSupported ?? false;
        public TimeSpan CurrentPositionUI
        {
            get
            {
                if (seekLogic.IsSeekAccumulationInProgress == false && seekLogic.IsSeekInProgress == false)
                    currentPosition = CurrentPositionPlayer;
                return currentPosition;
            }
            set => currentPosition = value;
        }
        private TimeSpan currentPosition;
        List<StreamDescription>[] allStreamsDescriptions = { null, null, null };

        public JuvoPlayerModule(ReactContext reactContext)
            : base(reactContext)
        {
            syncContext = new SynchronizationContext();
            seekLogic = new SeekLogic(this);
        }
        private void InitializeJuvoPlayer()
        {
            juvoPlayer = new PlayerServiceProxy(new PlayerServiceImpl(window));
            juvoPlayer.StateChanged()
               .ObserveOn(syncContext)
               .Subscribe(OnPlayerStateChanged, OnPlaybackCompleted);
            juvoPlayer.PlaybackError()
                .ObserveOn(syncContext)
                .Subscribe(message =>
                {
                    var param = new JObject();
                    param.Add("Message", message);
                    SendEvent("onPlaybackError", param);
                });
            juvoPlayer.BufferingProgress()
                .ObserveOn(syncContext)
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
                    juvoPlayer.Start();
                    playbackTimer = new Timer(
                        callback: new TimerCallback(UpdatePlayTime),
                        state: CurrentPositionUI,
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
            if (juvoPlayer?.CurrentCueText != null)
            {
                txt = juvoPlayer?.CurrentCueText;
            }
            var param = new JObject();
            param.Add("Total", (int)Duration.TotalMilliseconds);
            param.Add("Current", (int)CurrentPositionUI.TotalMilliseconds);
            param.Add("SubtiteText", txt);
            SendEvent("onUpdatePlayTime", param);
        }
        void PlayJuvoPlayer(String videoSourceURI, String licenseServerURI, String drmScheme, PlayerServiceProxy player, string streamingProtocol)
        {
            var drmData = new List<DRMDescription>();
            if (licenseServerURI != null)
            {
                drmData.Add(new DRMDescription
                {
                    Scheme = drmScheme,
                    LicenceUrl = licenseServerURI,
                    KeyRequestProperties = new Dictionary<string, string>() { { "Content-Type", "text/xml; charset=utf-8" } },
                });
            }
            player.SetSource(new ClipDefinition
            {
                Type = streamingProtocol,
                Url = videoSourceURI,
                Subtitles = new List<SubtitleInfo>(),
                DRMDatas = drmData
            });
        }
        private void Play(string videoURI, string licenseURI, string DRM, string streamingProtocol)
        {
            try
            {
                if (videoURI == null) return;
                if (juvoPlayer?.State == PlayerState.Playing) return;
                InitializeJuvoPlayer();
                PlayJuvoPlayer(videoURI, licenseURI, DRM, juvoPlayer, streamingProtocol);
            }
            catch (Exception e)
            {
                Logger?.Error(Tag, e.Message);
            }
        }
        public Task Seek(TimeSpan to)
        {
            var param = new JObject();
            param.Add("To", (int)to.TotalMilliseconds);
            SendEvent("onSeek", param);
            return juvoPlayer?.SeekTo(to);
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
                this.allStreamsDescriptions[StreamTypeIndex].AddRange(juvoPlayer.GetStreamsDescription((StreamType)StreamTypeIndex));
            }
            else
            {
                this.allStreamsDescriptions[StreamTypeIndex] = juvoPlayer.GetStreamsDescription((StreamType)StreamTypeIndex);
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
                        juvoPlayer.DeactivateStream(StreamType.Subtitle);
                        return;
                    }
                }
                var stream = (StreamDescription)this.allStreamsDescriptions[StreamTypeIndex][SelectedIndex];
                juvoPlayer.ChangeActiveStream(stream);
            }
        }
        [ReactMethod]
        public void Log(string message)
        {
            Logger?.Info(message);
        }
        [ReactMethod]
        public void StartPlayback(string videoURI, string licenseURI, string DRM, string streamingProtocol)
        {
            Play(videoURI, licenseURI, DRM, streamingProtocol);
            seekLogic.IsSeekInProgress = false;
        }
        [ReactMethod]
        public void StopPlayback()
        {
            juvoPlayer?.Stop();
            juvoPlayer?.Dispose();
            juvoPlayer = null;
            seekLogic.IsSeekAccumulationInProgress = false;
        }
        [ReactMethod]
        public void PauseResumePlayback()
        {
            switch (juvoPlayer.State)
            {
                case JuvoPlayer.Common.PlayerState.Playing:
                    juvoPlayer?.Pause();
                    break;
                case JuvoPlayer.Common.PlayerState.Paused:
                    juvoPlayer?.Start();
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