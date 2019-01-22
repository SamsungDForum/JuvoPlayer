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
using System.Runtime.CompilerServices;

namespace JuvoLogger
{
    public interface ILogger
    {
        void Verbose(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Debug(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Info(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Warn(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Warn(Exception ex, string message = "", [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Error(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Error(Exception ex, string message = "", [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Fatal(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Fatal(Exception ex, string message = "", [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        bool IsLevelEnabled(LogLevel level);
    }
}
