/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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

using ElmSharp;
using JuvoLogger;
using JuvoLogger.Tizen;
using JuvoPlayer.Platforms.Tizen;
using Tizen.Applications;
using Window = ElmSharp.Window;

namespace JuvoPlayer.RESTful
{
    public class Program : CoreUIApplication
    {
        private Window _mainWindow;
        private HttpPlayer _httpPlayer;

        protected override void OnCreate()
        {
            base.OnCreate();

            _mainWindow = new Window("Main Window") {Geometry = new Rect(0, 0, 1920, 1080)};
            _mainWindow.Show();
            AppContext.Instance.MainWindow = _mainWindow;
            _httpPlayer = new HttpPlayer(9998);
        }

        protected override void OnTerminate()
        {
            _httpPlayer.Dispose();
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerBuilder()
                .WithLevel(LogLevel.Debug)
                .WithChannel("JuvoPlayer.RESTful")
                .WithTizenSink()
                .Build();
            PlatformTizen.Init();
            var app = new Program();
            app.Run(args);
        }
    }
}