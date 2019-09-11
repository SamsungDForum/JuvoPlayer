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
using Tizen.Applications;

namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private PlayerServiceProxy juvoPlayer;        
        private readonly TimeSpan UpdatePlaybackInterval = TimeSpan.FromMilliseconds(100);
        private static Timer playbackTimer ;
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

        public JuvoPlayerModule(ReactContext reactContext)
            : base(reactContext)
        {
            syncContext = new SynchronizationContext();
            seekLogic = new SeekLogic(this);
        }
        private void InitializeJuvoPlayer()
        {
            // You see a gray background and no video it means that the Canvas.cs file of the react-native-tizen framework is invalid.
            //It requires the change: BackgroundColor = Color.Transparent of the canvas class.
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

                    Logger?.Info($"Playback Error: {message}");
                    stopPlayback();
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
                Logger?.Info("keyDown.On = " + e.KeyName);
                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyDown", param);
            };
            _keyUp = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyUp, EcoreKeyEventArgs.Create);
            _keyUp.On += (s, e)  => {
                Logger?.Info("keyUp.On = " + e.KeyName);
                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyUp", param);
            };
        }
        private void OnPlayerStateChanged(PlayerState state)
        {
            Logger?.Info($"OnPlayerStateChanged: {state}");
            var param = new JObject();
            string value = "Idle";
            switch (state)
            {
                case PlayerState.Prepared:
                    juvoPlayer.Start();
                    playbackTimer = new Timer(
                        callback: new TimerCallback(UpdatePlayTime),
                        state: CurrentPositionUI,
                        dueTime: 0,
                        period: 1000);
                    value = "Prepared";
                    break;
                case PlayerState.Playing:
                    value = "Playing";
                    playbackTimer.Change(0, 1000); //resume progress info update
                    break;
                case PlayerState.Paused:
                    value = "Paused";
                    playbackTimer.Change(Timeout.Infinite, Timeout.Infinite); //suspend progress info update
                    break;
            }
            param.Add("State", value);
            SendEvent("onPlayerStateChanged", param);
            Logger?.Info("OnPlayerStateChanged: SendEvent attached");
        }
        private void OnPlaybackCompleted()
        {
            Logger?.Info("OnPlaybackCompleted...");
            var param = new JObject();
            SendEvent("onPlaybackCompleted", param);
            stopPlayback();
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
            Logger?.Info("Update buffering");
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", (int)percent);
            SendEvent("onUpdateBufferingProgress", param);
        }
        private void UpdatePlayTime(object timerState) 
        {
            Logger?.Info("UpdatePlayTime");
            //Propagate the bufffering progress event to JavaScript module           
            var param = new JObject();
            param.Add("Total", (int)Duration.TotalMilliseconds);
            param.Add("Current", (int)CurrentPositionUI.TotalMilliseconds);
            param.Add("Seeking", (int)((seekLogic.IsSeekInProgress || seekLogic.IsSeekAccumulationInProgress) ? 1 : 0));
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
        private async Task Play(string videoURI, string licenseURI, string DRM, string streamingProtocol)
        {
            try
            {
                if (videoURI == null) return;
                InitializeJuvoPlayer();
                if (juvoPlayer.State == PlayerState.Playing) return;
                PlayJuvoPlayer(videoURI, licenseURI, DRM, juvoPlayer, streamingProtocol);
                Logger?.Info("JuvoPlayerModule: Playback OK!");
            }
            catch (Exception e)
            {
                Log.Error(Tag, e.Message);
            }
        }
        public Task Seek(TimeSpan to)
        {
            Logger?.Info("Seek to.. " + to);
            var param = new JObject();
            param.Add("to", (int)to.TotalMilliseconds);
            SendEvent("onSeek", param);
            seekLogic.IsSeekAccumulationInProgress = false;
            return juvoPlayer?.SeekTo(to);
        }
       
        //ReactMethods - JS methods
        [ReactMethod]
        public async void log(string message)
        {
            Logger?.Info(message);
        }
        [ReactMethod]
        public async void startPlayback(string videoURI, string licenseURI, string DRM, string streamingProtocol)
        {
            Logger?.Info("JuvoPlayerModule startPlayback() function called! videoURI = " + videoURI + " licenseURI = " + licenseURI + " DRM = " + DRM + " streamingProtocol" + streamingProtocol);
            await Play(videoURI, licenseURI, DRM, streamingProtocol);
            seekLogic.IsSeekInProgress = false;
        }
        [ReactMethod]
        public void stopPlayback()
        {
            Logger?.Info("Stopping player");
            juvoPlayer?.Stop();
            juvoPlayer?.Dispose();
            playbackTimer?.Dispose();
            juvoPlayer = null;
            seekLogic.IsSeekAccumulationInProgress = false;
        }
        [ReactMethod]
        public void pauseResumePlayback()
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
        public void exitApp()
        {
            Logger?.Info("Exiting App...");
            ReactNativeApp app = (ReactNativeApp)Application.Current;
            app.ShutDown();
        }
        [ReactMethod]
        public void forward()
        {
            seekLogic.SeekForward();
        }
        [ReactMethod]
        public void rewind()
        {
            seekLogic.SeekBackward();
        }
    }
}