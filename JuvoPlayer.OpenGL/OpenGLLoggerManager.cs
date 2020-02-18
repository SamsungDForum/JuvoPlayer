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
using System.IO;
using System.Reflection;
using System.Threading;
using JuvoLogger;
using JuvoLogger.Tizen;

namespace JuvoPlayer.OpenGL
{
    class OpenGlLoggerManager : LoggerManager
    {
        private static SynchronizationContext _uiContext = null;

        private static LoggerBase CreateLogger(string channel, LogLevel level)
        {
            if (_uiContext == null)
                throw new NullReferenceException("OpenGLLoggerManager must be first configured with proper UI SynchronizationContext object!");

            var composite = new CompositeLogger(channel, level);
            composite.Add(new TizenLogger(channel, level));
            composite.Add(new OpenGlLogger(channel, level, _uiContext));
            return composite;
        }

        public static void Configure(Stream stream, SynchronizationContext uiContext)
        {
            _uiContext = uiContext;
            Configure(stream, CreateLogger);
        }

        public static void Configure(string contents, SynchronizationContext uiContext)
        {
            _uiContext = uiContext;
            Configure(contents, CreateLogger);
        }

        public static void Configure(SynchronizationContext uiContext)
        {
            var configFilename = Path.Combine(
                Path.GetDirectoryName(Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)),
                "res", "logger.config");

            var contents = string.Empty;
            if (File.Exists(configFilename))
            {
                contents = File.ReadAllText(configFilename);
            }
            Configure(contents, uiContext);
        }
    }
}
