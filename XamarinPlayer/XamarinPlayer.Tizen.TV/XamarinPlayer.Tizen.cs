using System;
using System.Text;
using ElmSharp;
using JuvoLogger.Tizen;
using JuvoLogger;
using Tizen;
using Tizen.Applications;
using XamarinPlayer.Services;


namespace XamarinPlayer.Tizen
{
    

    class Program : global::Xamarin.Forms.Platform.Tizen.FormsApplication, IKeyEventSender, IPreviewPayloadEventSender
    {
        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
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

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            // Handle the launch request, show the user the task requested through the "AppControlReceivedEventArgs" parameter
            // Smart Hub Preview function requires the below code to identify which deeplink have to be launched            
            ReceivedAppControl receivedAppControl = e.ReceivedAppControl;
            //fetch the JSON metadata defined on the smart Hub preview web server            
            receivedAppControl.ExtraData.TryGet("PAYLOAD", out string payload);
            //Logger.Info("The PAYLOAD value: " + payload);

            //If launched without the SmartHub Preview tile, the message string is null. 
            if (!string.IsNullOrEmpty(payload))
            {
                char[] charSeparator = new char[] { '&' };
                string[] result = payload.Split(charSeparator, StringSplitOptions.None);
                if (result.Length > 0)
                    Xamarin.Forms.MessagingCenter.Send<IPreviewPayloadEventSender, string>(this, "PayloadSent", result[0]);
            }

            base.OnAppControlReceived(e);
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
