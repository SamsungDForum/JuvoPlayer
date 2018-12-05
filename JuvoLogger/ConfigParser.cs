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
using System.IO;

namespace JuvoLogger
{
    public class ConfigParser
    {
        public Dictionary<string, LogLevel> LoggingLevels { get; } 

        public ConfigParser(string contents)
        {
            if (contents == null)
                throw new ArgumentNullException();

            LoggingLevels = new Dictionary<string, LogLevel>();

            using (var reader = new StringReader(contents))
            {
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    var splitLine = line.Split('=');
                    if (splitLine.Length != 2)
                        continue;
                    var channel = splitLine[0];
                    var levelString = splitLine[1];

                    if (Enum.TryParse(levelString, true, out LogLevel level)) LoggingLevels[channel] = level;
                }
            }
        }
    }
}
