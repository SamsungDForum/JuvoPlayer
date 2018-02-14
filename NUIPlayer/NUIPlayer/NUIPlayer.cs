using System;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using JuvoPlayer.DRM;
using JuvoPlayer.DRM.Cenc;
using JuvoPlayer.Player;
using JuvoPlayer.RTSP;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace NUIPlayer
{
    internal class Program : TVUIApplication
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private DataProviderFactoryManager dataProviders;

        private IDataProvider dataProvider;
        private IPlayerController playerController;

        private View myView;
        private TextLabel myText;

        protected override void OnCreate()
        {
            base.OnCreate();

            CreateUI();
            InitializeJuvoPlayer();
        }

        private void CreateUI()
        {
            Window.Instance.BackgroundColor = Color.Transparent; // !!important

            //Create a View instance and add it to the stage
            myView = new View();
            myView.Size2D = new Size2D(300, 200);
            myView.BackgroundColor = new Color(1.0f, 1.0f, 1.0f, 0.1f);
            myView.Position = new Position(810, 440, 0);
            //Subscribe Key Event
            myView.Focusable = true;
            myView.KeyEvent += KeyEvent;

            Window.Instance.GetDefaultLayer().Add(myView);

            //Create a child view and add it to the parent view.
            myText = new TextLabel("JuvoPlayer")
            {
                Position = new Position(40, 80, 0),
                TextColor = Color.Black,
                PointSize = 40
            };

            myView.Add(myText);

            Window.Instance.BackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

            FocusManager.Instance.SetCurrentFocusView(myView);
        }

        private void InitializeJuvoPlayer()
        {
            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            var drmManager = new DRMManager();
            drmManager.RegisterDrmHandler(new CencHandler());

            var playerAdapter = new SMPlayerAdapter();
            playerController = new PlayerController(playerAdapter, drmManager);
            playerController.TimeUpdated += OnTimeUpdated;
            playerController.PlaybackCompleted += OnPlaybackCompleted;
            playerController.ShowSubtitle += OnRenderSubtitle;
        }

        // method executed when clip is selected
        private void ShowClip(ClipDefinition clip)
        {
            ControllerConnector.DisconnectDataProvider(playerController, dataProvider);

            dataProvider = dataProviders.CreateDataProvider(clip);

            ControllerConnector.ConnectDataProvider(playerController, dataProvider);

            dataProvider.Start();
        }

        private void OnPlaybackCompleted()
        {
        }

        private void OnRenderSubtitle(Subtitle subtitle)
        {
        }

        public void OnTimeUpdated(TimeSpan time)
        {
        }

        private bool KeyEvent(object source, View.KeyEventArgs e)
        {
            if (playerController == null)
                return false;

            if (e.Key.State == Key.StateType.Down)
            {
                Logger.Info(e.Key.KeyPressedName);

                switch (e.Key.KeyPressedName)
                {
                    case "Left":
                    case "XF86AudioRewind":
//                        playerController.OnSeek();
                        return true;
                    case "Right":
                    case "XF86AudioNext":
//                        playerController.OnSeek();
                        return true;
                    case "Up":
                    case "Down":
                        return true;
                    case "Return":
                    case "XF86PlayBack":
                    case "XF86AudioPlay":
                        playerController.OnPlay();
                        return true;
                    case "XF86AudioPause":
                        playerController.OnPause();
                        return true;
                    case "XF86Back":
                    case "XF86AudioStop":
                        playerController.OnStop();
                        return true;
                    case "1":
                        myText.TextColor = Color.White;

                        ClipDefinition clip1 = new ClipDefinition()
                        {
                            Type = "RTSP",
                            Url = "rtsp://192.168.137.200/2kkk.ts"

                        };

                        ShowClip(clip1);
                        return true;
                    case "2":
                        myText.TextColor = Color.Black;

                        ClipDefinition clip2 = new ClipDefinition()
                        {
                            Type = "RTSP",
                            Url = "test"

                        };

                        ShowClip(clip2);
                        return true;
                    case "XF86Color":
                    case "XF86Red":
                    case "XF86Green":
                    case "XF86Blue":
                    case "XF86Yellow":
                        return true;
                    case "Escape":
                        Application.Current.Exit();
                        return true;
                }
            }

            return false;
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            //This function is called when application is launched.
            base.OnAppControlReceived(e);
        }

        protected override void OnPause()
        {
            //This function is called when the window's visibility is changed from visible to invisible.
            base.OnPause();
        }

        protected override void OnResume()
        {
            //This function is called when the window's visibility is changed from invisible to visible.
            base.OnResume();
        }

        protected override void OnTerminate()
        {
            //This function is called when the app exit normally.
            base.OnTerminate();
        }

        protected override void OnLowMemory(LowMemoryEventArgs e)
        {
            //This function is called when the system is low on memory.
            base.OnLowMemory(e);
        }

        protected override void OnLocaleChanged(LocaleChangedEventArgs e)
        {
            //This function is called when the language is changed.
            base.OnLocaleChanged(e);
        }

        static void UnhandledException(object sender, UnhandledExceptionEventArgs evt)
        {
            if (evt.ExceptionObject is Exception e)
            {
                Logger.Error(e.Message);
                Logger.Error(e.StackTrace);
            }
            else
            {
                Logger.Error("Got unhandled exception event: " + evt);
            }
        }

        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            //Create an Application
            Program myProgram = new Program();
            myProgram.Run(args);
        }
    }
}
