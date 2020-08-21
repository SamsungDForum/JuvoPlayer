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
using System.Collections.Generic;
using System.Linq;
using IniParser.Model;
using IniParser.Parser;

namespace JuvoLogger
{
    public class ConfigParser
    {
        public Dictionary<string, LogLevel> LoggingLevels { get; }

        private Dictionary<string, LogLevel> CreateDictionary(in IniData contents) =>
            contents["LogLevel"].ToDictionary(
                entry => entry.KeyName,
                entry => Enum.TryParse(entry.Value, true, out LogLevel level) ? level : LogLevel.Info);

        public ConfigParser(in IniData contents)
        {
            if (contents == null)
                throw new ArgumentNullException();

            LoggingLevels = CreateDictionary(contents);
        }

        public ConfigParser(string contents)
        {
            if (contents == null)
                throw new ArgumentNullException();

            try
            {
                var parser = new IniDataParser();
                LoggingLevels = CreateDictionary(parser.Parse(contents));
            }
            catch (IniParser.Exceptions.ParsingException)
            {
                LoggingLevels = new Dictionary<string, LogLevel>();
            }
        }
    }
}
