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
using Tizen.Multimedia;
using ElmSharp;
using ReactNative.Modules.Core;
using Newtonsoft.Json.Linq;
using Tizen.Applications;

namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener
    {
        private PlayerServiceProxy juvoPlayer;
        //private TVMultimedia.Player platformPlayer;

        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";
        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        EcoreEvent<EcoreKeyEventArgs> _keyUp;

        public JuvoPlayerModule(ReactContext reactContext)
            : base(reactContext)
        {
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
        public void OnDestroy()
        {
            Log.Error(Tag, "Destroying JuvoPlayerModule...");
            juvoPlayer.Dispose();
            //platformPlayer.Dispose();
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
            param.Add("Percent", percent);
            SendEvent("onUpdateBufferingProgress", param);
        }

        void PlayJuvoPlayerRTSP(String videoSourceURL, PlayerServiceProxy player)
        {
            try
            {
                player.SetSource(new ClipDefinition
                {
                    Title = "Title",
                    Type = "rtsp",
                    Url = videoSourceURL,
                    Subtitles = new List<SubtitleInfo>(),
                    Poster = "Poster",
                    Description = "Descritption",
                    DRMDatas = new List<DRMDescription>()
                });
                //Log.Error(Tag, "JuvoPlayerModule (PlayJuvoPlayerClean) player source set!");

                SynchronizationContext scx = new SynchronizationContext();

                player.StateChanged()
                    .ObserveOn(scx)
                    .Where(state => state == JuvoPlayer.Common.PlayerState.Prepared)
                    .Subscribe(state =>
                    {
                        player.Start();
                    });

                player.PlaybackError()
                    .ObserveOn(scx)
                    .Subscribe(message =>
                    {
                        Logger?.Info($"Playback Error: {message}");
                        //ReturnToMainMenu();
                        //DisplayAlert("Playback Error", message, "OK");
                    });

                player.BufferingProgress()
                    .ObserveOn(scx)
                    .Subscribe(UpdateBufferingProgress);
                //Log.Error(Tag, "JuvoPlayerModule (PlayJuvoPlayerClean) player statechanged()!");
            }
            catch (Exception e)
            {
                Log.Error(Tag, "PlayJuvoPlayerClean: " + e.Message + " stack trace: " + e.StackTrace);
            }
        }

        private void PlayJuvoPlayerClean(String videoSourceURL, PlayerServiceProxy player)
        {
            try
            {
                player.SetSource(new ClipDefinition
                {
                    Title = "Title",
                    Type = "dash",
                    Url = videoSourceURL,
                    Subtitles = new List<SubtitleInfo>(),
                    Poster = "Poster",
                    Description = "Descritption",
                    DRMDatas = new List<DRMDescription>()
                });

                SynchronizationContext scx = new SynchronizationContext();

                player.StateChanged()
                    .ObserveOn(scx)
                    .Where(state => state == JuvoPlayer.Common.PlayerState.Prepared)
                    .Subscribe(state =>
                    {
                        player.Start();
                    });

                player.PlaybackError()
                    .ObserveOn(scx)
                    .Subscribe(message =>
                    {
                        Logger?.Info($"Playback Error: {message}");
                        stopPlayback();
                    });

                player.BufferingProgress()
                    .ObserveOn(scx)
                    .Subscribe(UpdateBufferingProgress); 
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
                Title = "Title",
                Type = "dash",
                Url = videoSourceURL,
                Subtitles = new List<SubtitleInfo>(),
                Poster = "Poster",
                Description = "Descritption",
                DRMDatas = drmData
            });

            player.StateChanged()
               .ObserveOn(new SynchronizationContext())
               .Where(state => state == JuvoPlayer.Common.PlayerState.Prepared)
               .Subscribe(state =>
               {
                   player.Start();
               });
        }

        //Playback launching functions
        async Task PlayPlatformMediaClean(String videoSourceURL, TVMultimedia.Player player)
        {
            player.SetSource(new MediaUriSource(videoSourceURL));
            await player.PrepareAsync();
            player.Start();
        }

        private async Task Play(string videoURI, string licenseURI, string DRM)
        {
            try
            {
                Log.Error(Tag, "JuvoPlayerModule (Play) Play() launched..");
                var window = ReactProgram.RctWindow as Window; //The main window of the application has to be transparent.
                // You see a gray background and no video it means that the Canvas.cs file of the react-native-tizen framework is invalid.

                Log.Error(Tag, "JuvoPlayerModule (Play) window.Show()");
                /////////////Clean contents////////////////////
                var url = "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/car-20120827-manifest.mpd";
                if (videoURI != null) url = videoURI;
                //var url = "https://bitdash-a.akamaihd.net/content/sintel/sintel.mpd";
                //var url = "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";
                //var url = "http://wowzaec2demo.streamlock.net/live/bigbuckbunny/manifest_mvtime.mpd";
                //var url = "http://download.tsi.telecom-paristech.fr/gpac/dataset/dash/uhd/dashevc-live-2s/dashevc-live-2s-4k.mpd";
                //var url = "rtsp://192.168.137.187/canimals.ts";


                /////////////Play Ready encrypted content//////
                //var url = "http://profficialsite.origin.mediaservices.windows.net/c51358ea-9a5e-4322-8951-897d640fdfd7/tearsofsteel_4k.ism/manifest(format=mpd-time-csf)";
                //var license = "http://playready-testserver.azurewebsites.net/rightsmanager.asmx?PlayRight=1&UseSimpleNonPersistentLicense=1";
                //var url = "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/oops_cenc-20121114-signedlicenseurl-manifest.mpd";
                //var license = ""; //The license url is embeded in the video source .mpd file above

                /////////////Widevine encrypted content////////
                //var url = "https://bitmovin-a.akamaihd.net/content/art-of-motion_drm/mpds/11331.mpd";
                //var license = "https://widevine-proxy.appspot.com/proxy";
                //var url = "https://storage.googleapis.com/wvmedia/cenc/h264/tears/tears_uhd.mpd";
                //var license = "https://proxy.uat.widevine.com/proxy?provider=widevine_test";
                Log.Error(Tag, "JuvoPlayerModule (Play) url: " + url);

                //////The TV platform MediaPlayer (URL data source only).
                //platformPlayer = new TVMultimedia.Player { Display = new Display(window) };
                //await PlayPlatformMediaClean(url, platformPlayer);
                //await PlayPlatformMediaDRMed(url, license, platformPlayer);


                //////The JuvoPlayer backend (elementary stream data source).
                juvoPlayer = new PlayerServiceProxy(new PlayerServiceImpl(window));
                Log.Error(Tag, "JuvoPlayerModule (Play) juvoPlayer object created..");
                //PlayJuvoPlayerRTSP(url, juvoPlayer);
                if (videoURI == null) return;

                if (licenseURI == null)
                {
                    PlayJuvoPlayerClean(url, juvoPlayer);
                } else
                {
                    PlayJuvoPlayerDRMed(url, licenseURI, DRM, juvoPlayer);
                }                

                //PlayJuvoPlayerDRMed(url, license, "playready", juvoPlayer);
                //PlayJuvoPlayerDRMed(url, license, "widevine", juvoPlayer);
                Log.Error(Tag, "JuvoPlayerModule: Playback OK!");
            }
            catch (Exception e)
            {
                Log.Error(Tag, e.Message);
            }
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
            Logger?.Info("Closing player");
            juvoPlayer?.Stop();
            juvoPlayer?.Dispose();
            juvoPlayer = null;
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
        void exitApp()
        {
            Log.Error(Tag, "Exiting App...");
            ReactNativeApp app = (ReactNativeApp)Application.Current;
            app.ShutDown();
        }
        //public Task Seek(TimeSpan to)
        //{
        //    Newtonsoft.Json.Linq.JObject value;
        //    string json = @"{to: '2'}";
        //    value = JObject.Parse(json);
        //    Task<void> task = new Task<void>(SendEvent)<string>("SeekToTimeSpan")<JObject>(value);
        //    return task;
        //    //throw new NotImplementedException();
        //}
    }
}