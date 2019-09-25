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
            //Propagate the bufffering progress event to JavaScript module
            var param = new JObject();
            param.Add("Percent", (int)percent);
            SendEvent("onUpdateBufferingProgress", param);
        }
        private void UpdatePlayTime(object timerState) 
        {
            //Propagate the bufffering progress event to JavaScript module 
            var param = new JObject();
            param.Add("Total", (int)Duration.TotalMilliseconds);
            param.Add("Current", (int)CurrentPositionUI.TotalMilliseconds);           
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
            var param = new JObject();
            param.Add("To", (int)to.TotalMilliseconds);
            SendEvent("onSeek", param);         
            Logger?.Info($"Seek to: {to}");
            return juvoPlayer?.SeekTo(to);
        }

        //private void BindStreamPicker(Picker picker, StreamType streamType)
        //{
        //    var streams = juvoPlayer.GetStreamsDescription(streamType);

        //    InitializePicker(picker, streams);

        //    SelectDefaultStreamForPicker(picker, streams);

        //    RegisterSelectedIndexChangeEventForPicker(picker);
        //}

        //private void BindSubtitleStreamPicker()
        //{
        //    var streams = new List<StreamDescription>
        //    {
        //        new StreamDescription
        //        {
        //            Default = true,
        //            Description = "off",
        //            Id = 0,
        //            StreamType = StreamType.Subtitle
        //        }
        //    };

        //    streams.AddRange(juvoPlayer.GetStreamsDescription(StreamType.Subtitle));

        //    InitializePicker(Subtitles, streams);

        //    SelectDefaultStreamForPicker(Subtitles, streams);

        //    Subtitles.SelectedIndexChanged += (sender, args) =>
        //    {
        //        if (Subtitles.SelectedIndex == -1)
        //            return;

        //        if (Subtitles.SelectedIndex == 0)
        //        {
        //            juvoPlayer.DeactivateStream(StreamType.Subtitle);
        //            return;
        //        }

        //        var stream = (StreamDescription)Subtitles.ItemsSource[Subtitles.SelectedIndex];
        //        try
        //        {
        //            juvoPlayer.ChangeActiveStream(stream);
        //        }
        //        catch (Exception ex)
        //        {
        //            Logger.Error(ex);
        //            Subtitles.SelectedIndex = 0;
        //        }
        //    };
        //}

        //private static void InitializePicker(Picker picker, List<StreamDescription> streams)
        //{
        //    picker.ItemsSource = streams;
        //    picker.ItemDisplayBinding = new Binding("Description");
        //    picker.SelectedIndex = 0;
        //}

        //private static void SelectDefaultStreamForPicker(Picker picker, List<StreamDescription> streams)
        //{
        //    for (var i = 0; i < streams.Count; ++i)
        //    {
        //        if (streams[i].Default)
        //        {
        //            picker.SelectedIndex = i;
        //            return;
        //        }
        //    }
        //}

        //private void RegisterSelectedIndexChangeEventForPicker(Picker picker)
        //{
        //    picker.SelectedIndexChanged += (sender, args) =>
        //    {
        //        if (picker.SelectedIndex != -1)
        //        {
        //            var stream = (StreamDescription)picker.ItemsSource[picker.SelectedIndex];

        //            _playerService.ChangeActiveStream(stream);
        //        }
        //    };
        //}


        //ReactMethods - JS methods
        [ReactMethod]
        public void getStreamsDescription(int StreamTypeIndex)
        {
            List<StreamDescription> streams;
            if (StreamTypeIndex == 2)  //Subtitle
            {
                streams = new List<StreamDescription>
                {
                    new StreamDescription
                    {
                        Default = true,
                        Description = "off",
                        Id = 0,
                        StreamType = (StreamType)StreamTypeIndex
                    }
                };
                streams.AddRange(juvoPlayer.GetStreamsDescription((StreamType)StreamTypeIndex));
            } else
            {
                streams = juvoPlayer.GetStreamsDescription((StreamType)StreamTypeIndex);
            }
            Logger?.Info($"getStreamsDescription StreamTypeIndex : {StreamTypeIndex}");
            var param = new JObject();
            param.Add("Description", Newtonsoft.Json.JsonConvert.SerializeObject(streams));
            param.Add("StreamTypeIndex", (int)StreamTypeIndex);
            SendEvent("onGotStreamsDescription", param);
        }
        [ReactMethod]
        public void log(string message)
        {
            Logger?.Info(message);
        }
        [ReactMethod]
        public async void startPlayback(string videoURI, string licenseURI, string DRM, string streamingProtocol)
        {
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