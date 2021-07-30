/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElmSharp;
using JuvoLogger;
using JuvoLogger.Tizen;
using JuvoPlayer.Platforms.Tizen;
using NUnit.Common;
using NUnitLite;
using Tizen.Applications;
using Window = ElmSharp.Window;

namespace JuvoPlayer.TizenTests
{
    internal class Program : CoreUIApplication
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("UT");
        private SynchronizationContext _mainSynchronizationContext;
        private Window _mainWindow;
        private string[] _nunitArgs;
        private ReceivedAppControl _receivedAppControl;
        private Label _testNameLabel;

        protected override void OnCreate()
        {
            base.OnCreate();

            _mainWindow = new Window("Main Window") {Geometry = new Rect(0, 0, 1920, 1080)};
            _mainWindow.Show();
            AppContext.Instance.MainWindow = _mainWindow;

            _testNameLabel = new Label(_mainWindow) {Color = Color.White};
            _testNameLabel.Show();

            _mainSynchronizationContext = SynchronizationContext.Current;
        }

        private void ExtractNunitArgs()
        {
            _nunitArgs = new string[0];
            if (_receivedAppControl.ExtraData.TryGet("--nunit-args", out string unparsed))
                _nunitArgs = unparsed.Split(":");
        }

        private void RunTests(Assembly assembly)
        {
            var sb = new StringBuilder();
            var dllName = assembly.ManifestModule.ToString();

            using (var writer = new ExtendedTextWrapper(new TextWriter(
                _testNameLabel,
                sb,
                _mainSynchronizationContext)))
            {
                var finalNunitArgs = _nunitArgs.Concat(new[]
                {
                    "--labels=Before", "--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml",
                    "--work=/tmp"
                }).ToArray();
                new AutoRun(assembly).Execute(finalNunitArgs, writer, Console.In);
            }

            foreach (var line in sb.ToString().Split("\n"))
                Logger.Info(line);
        }

        private void RunJuvoPlayerTizenTests()
        {
            RunTests(typeof(Program).GetTypeInfo().Assembly);
        }

        protected override async void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            _receivedAppControl = e.ReceivedAppControl;
            ExtractNunitArgs();
            await Task.Factory.StartNew(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                RunJuvoPlayerTizenTests();
            }, TaskCreationOptions.LongRunning);

            Exit();
        }

        private static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            PlatformTizen.Init();
            var program = new Program();
            program.Run(args);
        }

        private class TextWriter : System.IO.TextWriter
        {
            private readonly SynchronizationContext _mainSynchronizationContext;
            private readonly StringBuilder _stringBuilder;
            private readonly Label _testNameLabel;

            public TextWriter(
                Label testNameLabel,
                StringBuilder stringBuilder,
                SynchronizationContext mainSynchronizationContext)
            {
                _stringBuilder = stringBuilder;
                _testNameLabel = testNameLabel;
                _mainSynchronizationContext = mainSynchronizationContext;
            }

            public override Encoding Encoding => Encoding.Default;

            public override void Write(string value)
            {
                _stringBuilder.Append(value);

                if (value.StartsWith("=>"))
                {
                    _mainSynchronizationContext.Post(_ =>
                    {
                        _testNameLabel.Text = value;

                        var size = _testNameLabel.EdjeObject["elm.text"].TextBlockFormattedSize;
                        _testNameLabel.Resize(size.Width, size.Height);
                        _testNameLabel.Move(0, 1080 - size.Height);
                    }, null);
                }
            }
        }
    }
}