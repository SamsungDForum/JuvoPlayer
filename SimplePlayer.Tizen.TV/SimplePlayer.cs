using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading;
using Xamarin.Forms;
using JuvoPlayer;
using JuvoPlayer.Common;
using System.Collections.Generic;
using TVMultimedia = Tizen.TV.Multimedia;
using Tizen.Multimedia;

namespace SimplePlayer
{
    public class CodeButtonClickPage : ContentPage, IDisposable
    {
        private TVMultimedia.Player platformPlayer;
        private TVMultimedia.DRMManager platformDrmMgr;
        private PlayerServiceProxy<PlayerServiceImpl> juvoPlayer;

        public CodeButtonClickPage()
        {
            //Playback launching functions
            async Task PlayPlatformMediaClean(String videoSourceURL, TVMultimedia.Player player)
            {
                player.SetSource(new MediaUriSource(videoSourceURL));
                await player.PrepareAsync();
                player.Start();
            }

            async Task PlayPlatformMediaDRMed(String videoSourceURL, String licenseServerURL, TVMultimedia.Player player)
            {
                platformDrmMgr = TVMultimedia.DRMManager.CreateDRMManager(TVMultimedia.DRMType.Playready);

                platformDrmMgr.Init($"org.tizen.example.SimplePlayer.Tizen.TV");
                platformDrmMgr.AddProperty("LicenseServer", licenseServerURL);
                platformDrmMgr.Url = videoSourceURL;
                platformDrmMgr.Open();
                player.SetDrm(platformDrmMgr);

                await PlayPlatformMediaClean(videoSourceURL, player);
            }

            void PlayJuvoPlayerClean(String videoSourceURL, IPlayerService player)
            {
                player.SetSource(new ClipDefinition
                {
                    Title = "Title",
                    Type = "dash",
                    Url = videoSourceURL,
                    Subtitles = new List<SubtitleInfo>(),
                    Poster = "Poster",
                    Description = "Descritption",
                    DRMDatas = new List<DrmDescription>()
                });

                player.StateChanged()
                    .ObserveOn(SynchronizationContext.Current)
                    .Where(state => state == JuvoPlayer.Common.PlayerState.Prepared)
                    .Subscribe(state =>
                    {
                        player.Start();
                    });
            }

            void PlayJuvoPlayerDRMed(String videoSourceURL, String licenseServerURL, String drmScheme, IPlayerService player)
            {
                var drmData = new List<DrmDescription>();
                drmData.Add(new DrmDescription
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
                   .ObserveOn(SynchronizationContext.Current)
                   .Where(state => state == JuvoPlayer.Common.PlayerState.Prepared)
                   .Subscribe(state =>
                   {
                       player.Start();
                   });
            }

            async Task Play()
            {
                var window = new ElmSharp.Window("SimplePlayer")
                {
                    Geometry = new ElmSharp.Rect(0, 0, 1920, 1080)
                };
                window.Show();

                /////////////Clean contents////////////////////
                //var url = "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/car-20120827-manifest.mpd";
                //var url = "https://bitdash-a.akamaihd.net/content/sintel/sintel.mpd";
                //var url = "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";
                var url = "http://wowzaec2demo.streamlock.net/live/bigbuckbunny/manifest_mvtime.mpd";

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


                //////The TV platform MediaPlayer (URL data source only).
                //platformPlayer = new TVMultimedia.Player { Display = new Display(window) };
                //await PlayPlatformMediaClean(url, platformPlayer);
                //await PlayPlatformMediaDRMed(url, license, platformPlayer);

                //////The JuvoPlayer backend (elementary stream data source).
                juvoPlayer = new PlayerServiceProxy<PlayerServiceImpl>();
                juvoPlayer.SetWindow(window);
                PlayJuvoPlayerClean(url, juvoPlayer);
                //PlayJuvoPlayerDRMed(url, license, "playready", juvoPlayer);
                //PlayJuvoPlayerDRMed(url, license, "widevine", juvoPlayer);
            }

            //GUI contents initialization lines below
            Title = "Simple video player app";
            Label label = new Label
            {
                Text = "Please, press the 'enter' key",
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
            Content = new StackLayout
            {
                Children =
                 {
                    label,
                    button
                }
            };

            button.Clicked += async (sender, args) => await Play();
            Appearing += (sender, e) => button.Focus();
        }
        public void Dispose()
        {
            platformPlayer.Stop();
            platformPlayer.Unprepare();
            platformPlayer.Dispose();

            platformDrmMgr.Close();
            platformDrmMgr.Dispose();

            juvoPlayer.Stop();
            juvoPlayer.Dispose();
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
        }

        protected override void OnResume()
        {
        }
    }
}
