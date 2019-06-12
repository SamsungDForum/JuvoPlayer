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


namespace JuvoReactNative
{
    public class JuvoPlayerModule : ReactContextNativeModuleBase, ILifecycleEventListener, ISeekLogicClient
    {
        private PlayerServiceProxy juvoPlayer;
        private TVMultimedia.Player platformPlayer;

        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";

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

        public TimeSpan CurrentPositionUI { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TimeSpan CurrentPositionPlayer => juvoPlayer?.CurrentPosition ?? TimeSpan.Zero;

        public TimeSpan Duration => juvoPlayer?.Duration ?? TimeSpan.Zero;

        public JuvoPlayer.Common.PlayerState State => ((IPlayerService)juvoPlayer)?.State ?? JuvoPlayer.Common.PlayerState.Idle;

        public bool IsSeekingSupported => juvoPlayer?.IsSeekingSupported ?? false;


        public void OnDestroy()
        {
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
            //_bufferingProgress = percent;
            //_bufferingInProgress = percent < 100;
            //Log.Error(Tag, $"Buffering {(true ? $"in progress: {percent}%" : "ended")}.");
        }

        void PlayJuvoPlayerClean(String videoSourceURL, PlayerServiceProxy player)
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

        private async Task Play()
        {
            try
            {
                Log.Error(Tag, "JuvoPlayerModule (Play) Play() launched..");
                var window = ReactProgram.RctWindow as Window; //The main window of the application has to be transparent.
                // You see a gray background and no video it means that the Canvas.cs file of the react-native-tizen framework is invalid.

                Log.Error(Tag, "JuvoPlayerModule (Play) window.Show()");
                /////////////Clean contents////////////////////
                //var url = "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/car-20120827-manifest.mpd";
                //var url = "https://bitdash-a.akamaihd.net/content/sintel/sintel.mpd";
                //var url = "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";
                var url = "http://wowzaec2demo.streamlock.net/live/bigbuckbunny/manifest_mvtime.mpd";
                //var url = "http://download.tsi.telecom-paristech.fr/gpac/dataset/dash/uhd/dashevc-live-2s/dashevc-live-2s-4k.mpd";


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
                PlayJuvoPlayerClean(url, juvoPlayer);
                //PlayJuvoPlayerDRMed(url, license, "playready", juvoPlayer);
                //PlayJuvoPlayerDRMed(url, license, "widevine", juvoPlayer);
                Log.Error(Tag, "JuvoPlayerModule: Playback OK!");
            }
            catch (Exception e)
            {
                Log.Error(Tag, e.Message);
            }
        }

        [ReactMethod]
        public async void startPlayback()
        {
            //StartTimer(UpdateInterval, UpdatePlayerControl);

            Log.Error(Tag, "JuvoPlayerModule startPlayback() function called! ");
            await Play();
        }

        public Task Seek(TimeSpan to)
        {
            throw new NotImplementedException();
        }
    }
}