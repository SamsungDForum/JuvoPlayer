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
using JuvoLogger.Tizen;
using JuvoLogger.Udp;
using JuvoPlayer.Common;
using Tizen.Applications;
using Tizen.System;
using Xamarin.Forms;
using Xamarin.Forms.GenGridView.Tizen;
using Xamarin.Forms.Platform.Tizen;
using XamarinPlayer.Tizen.TV.Services;
using Log = Tizen.Log;
using Size = ElmSharp.Size;

namespace XamarinPlayer.Tizen.TV
{
    internal class Program : FormsApplication, IKeyEventSender
    {
        private EcoreEvent<EcoreKeyEventArgs> _keyDown;
        private App _app;
        private const string Tag = "JuvoPlayer";

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

            _app = new App();
            LoadApplication(_app);
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs evt)
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
            await _app.LoadUrl(url);
        }

        private static void Main(string[] args)
        {
            UdpLoggerManager.Configure();
            if(!UdpLoggerManager.IsRunning)
                TizenLoggerManager.Configure();
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            try
            {
                var app = new Program();

                GenGridView.Init();
                Forms.Init(app);
                app.Run(args);
            }
            finally
            {
                if (UdpLoggerManager.IsRunning)
                    UdpLoggerManager.Terminate();
            }
        }
    }
}
