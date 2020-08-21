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

using JuvoLogger;

namespace JuvoPlayer.Tests.UnitTests
{
    // This class is used to check logging properties, like function names and line numbers.
    // Test cases assume that each logging statement is done in fixed line.
    //
    // Please modify corresponding test cases if you would like to modify this file.
    public class LoggerClient
    {
        public ILogger Logger { get; }
        public static readonly string LogMessage = "message";
        public static readonly string FileName = "LoggerClient.cs";
        public static readonly int FuncFirstLineNumber = 40;

        public LoggerClient(ILogger logger)
        {
            this.Logger = logger;
        }

        public void Func()
        {
            Logger.Fatal(LogMessage);
            Logger.Error(LogMessage);
            Logger.Warn(LogMessage);
            Logger.Info(LogMessage);
            Logger.Debug(LogMessage);
            Logger.Verbose(LogMessage);
        }
    }
}
