using System;
using ElmSharp;
using Tizen;
using XamarinPlayer.Services;

namespace XamarinPlayer.Tizen
{
    class Program : global::Xamarin.Forms.Platform.Tizen.FormsApplication, IKeyEventSender
    {
        EcoreEvent<EcoreKeyEventArgs> _keyDown;

        public static readonly string Tag = "XamarinPlayer.Tizen";

        protected override void OnCreate()
        {
            base.OnCreate();

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
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            var app = new Program();
            global::Xamarin.Forms.Platform.Tizen.Forms.Init(app);
            app.Run(args);
        }
    }
}
