using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading;
using Xamarin.Forms;
using JuvoPlayer.Common;
using JuvoPlayer;



namespace SimplePlayer
{
    class PlayerService : PlayerServiceProxy
    {
        /// <summary>
        /// his class is required by the JuvoPlayer backend only
        /// </summary>
        public new PlayerState State => ToPlayerState(base.State);
        public PlayerService(ElmSharp.Window window)
           : base(new PlayerServiceImpl(window))
        {
        }
        public new IObservable<PlayerState> StateChanged()
        {
            return base.StateChanged().Select(ToPlayerState);
        }
        private PlayerState ToPlayerState(PlayerState state)
        {
            switch (state)
            {
                case JuvoPlayer.Common.PlayerState.Idle:
                    return PlayerState.Idle;
                case JuvoPlayer.Common.PlayerState.Prepared:
                    return PlayerState.Prepared;
                case JuvoPlayer.Common.PlayerState.Paused:
                    return PlayerState.Paused;
                case JuvoPlayer.Common.PlayerState.Playing:
                    return PlayerState.Playing;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

    }

    public class CodeButtonClickPage : ContentPage, IDisposable
    {
        private Tizen.TV.Multimedia.Player platformPlayer;
        private Tizen.TV.Multimedia.DRMManager platformDrmMgr;
        private PlayerService juvoPlayerService;

        public CodeButtonClickPage()
        {
            //Playback launching functions
            async Task PlayPlatformMediaClean(String URL, Tizen.TV.Multimedia.Player player)
            {
                player.SetSource(new Tizen.Multimedia.MediaUriSource(URL));
                await player.PrepareAsync();
                player.Start();
            }

            async Task PlayPlatformMediaDRMed(String URL, String licenseServerUrl, Tizen.TV.Multimedia.Player player)
            {
                platformDrmMgr = Tizen.TV.Multimedia.DRMManager.CreateDRMManager(Tizen.TV.Multimedia.DRMType.Playready);

                platformDrmMgr.Init($"org.tizen.example.SimplePlayer.Tizen.TV");
                platformDrmMgr.AddProperty("LicenseServer", licenseServerUrl);
                platformDrmMgr.Url = URL;
                platformDrmMgr.Open();
                player.SetDrm(platformDrmMgr);

                await PlayPlatformMediaClean(URL, player);
            }

            void PlayJuvoPlayerClean(String URL, ElmSharp.Window window)
            {
                juvoPlayerService = new PlayerService(window);

                juvoPlayerService.StateChanged()
                    .ObserveOn(SynchronizationContext.Current)
                    .Where(state => state == PlayerState.Prepared)
                    .Subscribe(state =>
                    {
                        juvoPlayerService.Start();
                    });

                juvoPlayerService.SetSource(new ClipDefinition
                {
                    Title = "Google Car",
                    Type = "dash",
                    Url = URL,
                    Subtitles = new System.Collections.Generic.List<SubtitleInfo>(),
                    Poster = "Poster",
                    Description = "Descritption",
                    DRMDatas = new System.Collections.Generic.List<DRMDescription>()
                });                
            }

            //private static PlayJuvoPlayerDRMed(String URL, String licenceUrl, ElmSharp.Window window)
            //{
            //    licenceUrl =
            //   "http://dash-mse-test.appspot.com/api/drm/playready?drm_system=playready&source=YOUTUBE&video_id=03681262dc412c06&ip=0.0.0.0&ipbits=0&expire=19000000000&sparams=ip,ipbits,expire,drm_system,source,video_id&signature=3BB038322E72D0B027F7233A733CD67D518AF675.2B7C39053DA46498D23F3BCB87596EF8FD8B1669&key=test_key1";
            //    var configuration = new DRMDescription()
            //    {
            //        Scheme = CencUtils.GetScheme(PlayreadySystemId),
            //        LicenceUrl = licenceUrl,
            //        KeyRequestProperties = new Dictionary<string, string>() { { "Content-Type", "text/xml; charset=utf-8" } },
            //    };
            //}

            async Task Play()
            {                
                var window = new ElmSharp.Window("SimplePlayer")
                {
                    Geometry = new ElmSharp.Rect(0, 0, 1920, 1080)
                };
                window.Show();

                //////Common TV platform MediaPlayer using the URL Data source only
                //platformPlayer = new Tizen.TV.Multimedia.Player { Display = new Tizen.Multimedia.Display(window) };

                /////////////Clean//////////////////////////////
                var url = "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/car-20120827-manifest.mpd";
                //"http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4"
               // await PlayPlatformMediaClean(url, platformPlayer);

                /////////////Play Ready//////////////////////////
                //"http://profficialsite.origin.mediaservices.windows.net/c51358ea-9a5e-4322-8951-897d640fdfd7/tearsofsteel_4k.ism/manifest(format=mpd-time-csf)"
                //license = "http://playready-testserver.azurewebsites.net/rightsmanager.asmx?PlayRight=1&UseSimpleNonPersistentLicense=1"
                //await PlayPlatformMediaDRMed(url, license, platformPlayer);

                ////////////The JuvoPlayer backend using Url and Elementary Stream data sources.
                PlayJuvoPlayerClean(url, window);               
            }

            //GUI contents initialization lines below
            Title = "Simple video player app";
            Label label = new Label
            {
                Text = "Press 'down key' followed by the 'enter key'",
                FontSize = Device.GetNamedSize(NamedSize.Large, typeof(Label)),
                VerticalOptions = LayoutOptions.CenterAndExpand,
                HorizontalOptions = LayoutOptions.Center
            };
            Button button = new Button
            {
                Text = "Click to play!",
                VerticalOptions = LayoutOptions.CenterAndExpand,
                HorizontalOptions = LayoutOptions.Center
            };
            button.Clicked += async (sender, args) => await Play();
            Content = new StackLayout
            {
                Children =
                 {
                    label,
                    button
                }
            };
        }

        public void Dispose()
        {            
            platformPlayer.Stop();
            platformPlayer.Unprepare();
            platformPlayer.Dispose();

            platformDrmMgr.Close();
            platformDrmMgr.Dispose();

            juvoPlayerService.Dispose();
        }
    }


    public class App : Application
    {
        public App()
        {
            MainPage = new CodeButtonClickPage();
        }

        protected override void OnStart()
        {

        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
