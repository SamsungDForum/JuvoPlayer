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
using JuvoLogger;

namespace JuvoPlayer.Utils
{
    // HexDump - Wheel reinvented.
    // was not too happy with "ready made" solutions... kind of an overkill...
    // https://www.codeproject.com/Articles/36747/Quick-and-Dirty-HexDump-of-a-Byte-Array
    // https://blogs.msdn.microsoft.com/ericwhite/2010/03/12/hex-dump-using-linq-in-7-lines-of-code/

    public static class DumpTools
    {
        public static void LineDump(this string data, ILogger logger, char lineSeparator='\n')
        {
            var src = data.Split(lineSeparator);
            foreach (var t in src)
            {
                logger.Info(t);
            }
        }

        /// <summary>
        /// Dumps last N bytes from byte array in human readable format.
        /// Method internally calls HexDump() method
        ///
        /// Can be used as standalone method or extension method
        ///
        /// </summary>
        /// <param name="bytes">Byte array to be displayed</param>
        /// <param name="length">Number of bytes to display from beginning of the buffer. First {length} bytes
        /// to display</param>
        /// <param name="doTextDump">Optional. True (default) - dump printable text along with hex dump
        /// False - Do not display text, just hex dump</param>
        /// <param name="bytesPerLine">Optional. 16 (default) - number of bytes to display per line</param>
        /// <returns>string containing hex dump of input</returns>
        public static string HexDumpLastN(this byte[] bytes, int length, bool doTextDump = true, int bytesPerLine = 16)
        {
            if (bytes == null) return "<null>";

            int idx = bytes.Length - 1 - length;
            int dlen = length;
            if (idx < 0)
            {
                idx = 0;
                dlen = bytes.Length;
            }

            return HexDump(bytes, idx, dlen, doTextDump, bytesPerLine);
        }

        /// <summary>
        /// Dumps first N bytes from byte array in human readable format.
        /// Method internally calls HexDump() method
        ///
        /// Can be used as standalone method or extension method
        ///
        /// </summary>
        /// <param name="bytes">Byte array to be displayed</param>
        /// <param name="length">Number of bytes to display from beginning of the buffer. First {length} bytes
        /// to display</param>
        /// <param name="doTextDump">Optional. True (default) - dump printable text along with hex dump
        /// False - Do not display text, just hex dump</param>
        /// <param name="bytesPerLine">Optional. 16 (default) - number of bytes to display per line</param>
        /// <returns>string containing hex dump of input</returns>
        public static string HexDumpFirstN(this byte[] bytes, int length, bool doTextDump = true, int bytesPerLine = 16)
        {
            return HexDump(bytes, 0, length, doTextDump, bytesPerLine);
        }

        /// <summary>
        /// Dumps byte array in a human readable form
        ///
        /// RRRRRR: DD... | Printable Characters or .
        ///
        /// RRRRR       - Offset Value
        /// DD          - Hex Data
        /// Printables  - Printable characters only
        ///
        /// Can be used as standalone method or extension method
        ///
        /// </summary>
        /// <param name="bytes">Byte array to be displayed</param>
        /// <param name="index">Optional. Start Index from which byte array is to be dumped.</param>
        /// <param name="length">Optional. Length of data to be dumped</param>
        /// <param name="doTextDump">Optional. True (default) - dump printable text along with hex dump
        /// False - Do not display text, just hex dump</param>
        /// <param name="bytesPerLine">Optional. 16 (default) - number of bytes to display per line</param>
        /// <returns>string containing hex dump of input</returns>
        public static string HexDump(this byte[] bytes, int? index = null, int? length = null, bool doTextDump = true, int bytesPerLine = 16)
        {
            return string.Join("\n", HexDumpEnumerable(bytes, index, length, doTextDump, bytesPerLine));
        }

        public static IEnumerable<string> HexDumpEnumerable(this byte[] bytes, int? index = null, int? length = null, bool doTextDump = true, int bytesPerLine = 16)
        {
            if (bytes == null)
            {
                yield return "<null>";
                yield break;
            }

            // compute default start index and byte range
            int startIndex = index ?? 0;
            int bytesLength = length ?? bytes.Length;

            // Adjust bytes to process based on start index and requested length.
            // If size of input is less then startindex + requested range, adjust it so data till end is
            // displayed from start.
            bytesLength = (bytes.Length < (startIndex + bytesLength)) ? bytes.Length - startIndex : bytesLength;

            // Validate if we are not requesting bytes beyond input array size
            if (bytesLength <= 0)
            {
                yield return "<No Data Requested or Index/Length out of input byte[] range>";
                yield break;
            }

            for (int i = startIndex; i < bytesLength;)
            {
                int bytesToDo = (bytesPerLine <= (bytesLength - i)) ? bytesPerLine : (bytesLength - i);

                // Convert a line of data into hex, replace "-" (as BitConverter generates)  with a space ' ' and pad
                // if data to be dumped is shorter then # of bytes per line.
                string hex = BitConverter.ToString(bytes, i, bytesToDo).Replace("-", " ").PadRight((bytesPerLine * 3) - 1);

                if (doTextDump)
                {
                    string text = "";

                    for (int j = i; j < bytesToDo + i; j++)
                    {
                        // Dump only "printable" characters. Range 0x20 to 0x7E inclusive
                        // All else is replaced with a '.'
                        if (bytes[j] > 0x1F && bytes[j] < 0x7F)
                        {
                            text += Convert.ToChar(bytes[j]) + " ";
                        }
                        else
                        {
                            text += ".";
                        }

                    }

                    yield return i.ToString("X6") + ": " + hex + " | " + text;
                }
                else
                {
                    yield return i.ToString("X6") + ": " + hex;
                }

                i += bytesToDo;
            }
        }
    }
}

