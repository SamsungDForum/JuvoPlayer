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
using TVMultimedia = Tizen.TV.Multimedia;
//using Tizen.Multimedia;
using ElmSharp;
using ReactNative.Modules.Core;
using Newtonsoft.Json.Linq;
using Tizen.Applications;

namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private PlayerServiceProxy juvoPlayer;
        private readonly int DefaultTimeout = 5000;
        private readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);

        private SeekLogic seekLogic = null; // needs to be initialized in constructor!
        //private TVMultimedia.Player platformPlayer;

        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";
        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        EcoreEvent<EcoreKeyEventArgs> _keyUp;
        SynchronizationContext syncContext;
        Window window = ReactProgram.RctWindow; // as Window; //The main window of the application has to be transparent.

        public JuvoPlayerModule(ReactContext reactContext)
            : base(reactContext)
        {
            syncContext = new SynchronizationContext();
            seekLogic = new SeekLogic(this);
        }

        private void InitializeJuvoPlayer()
        {
            // You see a gray background and no video it means that the Canvas.cs file of the react-native-tizen framework is invalid.
           
            juvoPlayer = new PlayerServiceProxy(new PlayerServiceImpl(window));

            juvoPlayer.StateChanged()
               .ObserveOn(syncContext)
               .Subscribe(OnPlayerStateChanged, OnPlaybackCompleted);

            juvoPlayer.PlaybackError()
                .ObserveOn(syncContext)
                .Subscribe(message =>
                {
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
                Log.Error(Tag, "keyDown.On = " + e.KeyName);                

                //Propagate the key press event to JavaScript module
                var param = new JObject();
                param.Add("KeyName", e.KeyName);
                param.Add("KeyCode", e.KeyCode);
                SendEvent("onTVKeyDown", param);
            };

            _keyUp = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyUp, EcoreKeyEventArgs.Create);
            _keyUp.On += (s, e)  => {
                Log.Error(Tag, "keyUp.On = " + e.KeyName);

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
                    if (juvoPlayer.IsSeekingSupported)
                    {
                    }
                    juvoPlayer.Start();
                    value = "Prepared";
                    break;
                case PlayerState.Playing:
                    value = "Playing";
                    break;
                case PlayerState.Paused:
                    value = "Paused";
                    break;
            }
            param.Add("State", value);
            SendEvent("onPlayerStateChanged", param);
            Logger?.Info("OnPlayerStateChanged: SendEvent attached");
        }

        private void OnPlaybackCompleted()
        {
            Log.Error(Tag, "OnPlaybackCompleted...");
            var param = new JObject();
            SendEvent("onPlaybackCompleted", param);

            stopPlayback();
        }

        public void OnDestroy()
        {
            Log.Error(Tag, "Destroying JuvoPlayerModule...");
        }
        public void OnResume()
        {
        }
        public void OnSuspend()
        {
        }
        private void UpdateBufferingProgress(int percent)
        {
            Log.Error(Tag, "Update buffering");
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", (int)percent);
            SendEvent("onUpdateBufferingProgress", param);
        }

        private void UpdatePlayTime()
        {
            Log.Error(Tag, "UpdatePlayTime");
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("CurrentPosition", (int)Duration.TotalMilliseconds);
            param.Add("Duration", (int)CurrentPositionUI.TotalMilliseconds);
            SendEvent("onUpdatePlayTime", param);
        }

        private void PlayJuvoPlayerClean(String videoSourceURL, PlayerServiceProxy player)
        {
            try
            {
                player.SetSource(new ClipDefinition
                {
                 //   Title = "Title",
                    Type = "dash",
                    Url = videoSourceURL,
                    Subtitles = new List<SubtitleInfo>(),
                //    Poster = "Poster",
                //    Description = "Descritption",
                    DRMDatas = new List<DRMDescription>()
                });
            }
            catch (Exception e)
            {
                Log.Error(Tag, "PlayJuvoPlayerClean: " + e.Message + " stack trace: " + e.StackTrace);
            }
        }

        void PlayJuvoPlayerDRMed(String videoSourceURL, String licenseServerURL, String drmScheme, PlayerServiceProxy player)
        {
            var drmData = new List<DRMDescription>();
            drmData.Add(new DRMDescription
            {
                Scheme = drmScheme,
                LicenceUrl = licenseServerURL,
                KeyRequestProperties = new Dictionary<string, string>() { { "Content-Type", "text/xml; charset=utf-8" } },
            });

            player.SetSource(new ClipDefinition
            {
             // Title = "Title",
              Type = "dash",
                Url = videoSourceURL,
                Subtitles = new List<SubtitleInfo>(),
            //    Poster = "Poster",
             //  Description = "Descritption",
                DRMDatas = drmData
            });
        }

        //Playback launching functions
        async Task PlayPlatformMediaClean(String videoSourceURL, TVMultimedia.Player player)
        {
            player.SetSource(new Tizen.Multimedia.MediaUriSource(videoSourceURL));
            await player.PrepareAsync();
            player.Start();
        }

        private async Task Play(string videoURI, string licenseURI, string DRM)
        {
            try
            {
                if (videoURI == null) return;

                InitializeJuvoPlayer();

                if (juvoPlayer.State == PlayerState.Playing) return;

                Log.Error(Tag, "JuvoPlayerModule (Play) juvoPlayer object created..");

                if (licenseURI == null)
                {
                    PlayJuvoPlayerClean(videoURI, juvoPlayer);
                } else
                {
                    PlayJuvoPlayerDRMed(videoURI, licenseURI, DRM, juvoPlayer);
                }
                Log.Error(Tag, "JuvoPlayerModule: Playback OK!");
            }
            catch (Exception e)
            {
                Log.Error(Tag, e.Message);
            }
        }

        public Task Seek(TimeSpan to)
        {
            Log.Error(Tag, "Seek to.. " + to);
            var param = new JObject();
            param.Add("to", (int)to.TotalMilliseconds);
            SendEvent("onSeek", param);
           
            return juvoPlayer?.SeekTo(to);
        }

        //ReactMethods - accessible with JavaScript
        [ReactMethod]
        public async void log(string message)
        {
            Log.Error(Tag, message);
        }
        [ReactMethod]
        public async void startPlayback(string videoURI, string licenseURI, string DRM)
        {
            //StartTimer(UpdateInterval, UpdatePlayerControl);
            Log.Error(Tag, "JuvoPlayerModule startPlayback() function called! videoURI = " + videoURI + " licenseURI = " + licenseURI + " DRM = " + DRM );
            await Play(videoURI, licenseURI, DRM);
        }
        [ReactMethod]
        public void stopPlayback()
        {
            Logger?.Info("Stopping player");
            juvoPlayer?.Stop();
            juvoPlayer?.Dispose();
            juvoPlayer = null;
            //platformPlayer.Dispose();
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
            Log.Error(Tag, "Exiting App...");
            ReactNativeApp app = (ReactNativeApp)Application.Current;
            app.ShutDown();
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