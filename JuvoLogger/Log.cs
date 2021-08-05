/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2021, Samsung Electronics Co., Ltd
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
using System.Runtime.CompilerServices;

namespace JuvoLogger
{
    public static class Log
    {
        public static ILogger Logger { get; set; }

        public static void Verbose(string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Verbose(message, file, method, line);
        }

        public static void Debug(string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Debug(message, file, method, line);
        }

        public static void Info(string message= "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Info(message, file, method, line);
        }

        public static void Warn(string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0)
        {
            Logger.Warn(message, file, method, line);
        }

        public static void Warn(Exception ex, string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Warn(ex, message, file, method, line);
        }

        public static void Error(string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0)
        {
            Logger.Error(message, file, method, line);
        }

        public static void Error(Exception ex, string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Error(ex, message, file, method, line);
        }

        public static void Fatal(string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0)
        {
            Logger.Fatal(message, file, method, line);
        }

        public static void Fatal(Exception ex, string message = "", [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Fatal(ex, message, file, method, line);
        }

        public static ILogger WithPrefix(string prefix) => Logger.CopyWithPrefix(prefix);

        public static ILogger WithChannel(string channel) => Logger.CopyWithChannel(channel);
    }
}