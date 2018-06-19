using System;
using System.Text;
using ElmSharp;
using JuvoLogger.Tizen;
using Tizen;
using XamarinPlayer.Services;

namespace XamarinPlayer.Tizen
{
    class Program : global::Xamarin.Forms.Platform.Tizen.FormsApplication, IKeyEventSender
    {
        EcoreEvent<EcoreKeyEventArgs> _keyDown;

        public static readonly string Tag = "JuvoPlayer";

        protected override void OnCreate()
        {
            base.OnCreate();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.Net.ServicePointManager.DefaultConnectionLimit = 100;

            _keyDown = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyDown, EcoreKeyEventArgs.Create);
            _keyDown.On += (s, e) =>
            {
                // Send key event to the portable project using MessagingCenter
                Xamarin.Forms.MessagingCenter.Send<IKeyEventSender, string>(this, "KeyDown", e.KeyName);
            };

            LoadApplication(new App());
        }

        static void UnhandledException(object sender, UnhandledExceptionEventArgs evt)
        {
            if (evt.ExceptionObject is Exception e)
            {
                if (e.InnerException != null)
                    e = e.InnerException;

                Log.Error(Tag, e.Message);
                Log.Error(Tag, e.StackTrace);
            }
            else
            {
                Log.Error(Tag, "Got unhandled exception event: " + evt);
            }
        }

        static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            var app = new Program();
            global::Xamarin.Forms.Platform.Tizen.Forms.Init(app);
            app.Run(args);
        }
    }
}
