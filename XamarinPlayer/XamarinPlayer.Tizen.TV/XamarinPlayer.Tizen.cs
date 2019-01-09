/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ElmSharp;
using JuvoLogger;
using JuvoLogger.Tizen;
using Newtonsoft.Json;
using Tizen.Applications;
using Tizen.System;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Tizen;
using XamarinPlayer.Services;
using ILogger = JuvoLogger.ILogger;
using Log = Tizen.Log;
using Size = ElmSharp.Size;

namespace XamarinPlayer.Tizen
{


    class Program : FormsApplication, IKeyEventSender
    {
        EcoreEvent<EcoreKeyEventArgs> _keyDown;
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public static readonly string Tag = "JuvoPlayer";
        private App app;

        private class PayloadParser
        {
            private readonly ReceivedAppControl receivedAppControl;
            private string payload;
            private string json;

            public PayloadParser(ReceivedAppControl receivedAppControl)
            {
                this.receivedAppControl = receivedAppControl;
            }

            public bool TryGetUrl(out string url)
            {
                url = string.Empty;

                if (!TryGetPayload()) return false;
                if (!TryGetJson()) return false;
                url = ParseJson();
                return true;
            }

            private bool TryGetPayload()
            {
                return receivedAppControl.ExtraData.TryGet("PAYLOAD", out payload);
            }

            private bool TryGetJson()
            {
                char[] charSeparator = { '&' };
                var result = payload.Split(charSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (result.Length <= 1)
                    return false;
                json = result[0];
                return true;
            }

            private string ParseJson()
            {
                var definition = new {values = ""};
                return JsonConvert.DeserializeAnonymousType(json, definition).values;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ServicePointManager.DefaultConnectionLimit = 100;

            _keyDown = new EcoreEvent<EcoreKeyEventArgs>(EcoreEventType.KeyDown, EcoreKeyEventArgs.Create);
            _keyDown.On += (s, e) =>
            {
                // Send key event to the portable project using MessagingCenter
                MessagingCenter.Send<IKeyEventSender, string>(this, "KeyDown", e.KeyName);
            };

            app = new App();
            LoadApplication(app);
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

        private Task WaitForMainWindowResize()
        {
            var tcs = new TaskCompletionSource<bool>();

            var screenSize = GetScreenSize();

            if (MainWindow.Geometry.Size != screenSize)
            {
                void Handler(object sender, EventArgs e)
                {
                    if (MainWindow.Geometry.Size != screenSize)
                        return;
                    MainWindow.Resized -= Handler;
                    tcs.SetResult(true);
                }

                MainWindow.Resized += Handler;
            }
            else
            {
                tcs.SetResult(true);
            }

            return tcs.Task;
        }

        private static Size GetScreenSize()
        {
            var screenSize = new Size();

            if (!Information.TryGetValue("http://tizen.org/feature/screen.width", out int width))
                return screenSize;
            if (!Information.TryGetValue("http://tizen.org/feature/screen.height", out int height))
                return screenSize;

            screenSize.Width = width;
            screenSize.Height = height;
            return screenSize;
        }

        protected override async void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            var payloadParser = new PayloadParser(e.ReceivedAppControl);
            if (!payloadParser.TryGetUrl(out var url))
                return;
            await WaitForMainWindowResize();
            await app.LoadUrl(url);
        }

        static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            var app = new Program();

            Forms.Init(app);
            app.Run(args);
        }
    }
}
