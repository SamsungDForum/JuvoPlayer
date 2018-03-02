// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using System;
using System.Collections.Generic;
using System.Text;


namespace JuvoPlayer.Common.Utils
{
    // HexDump - Wheel reinvented.
    // was not too happy with "ready made" solutions... kind of an overkill...
    // https://www.codeproject.com/Articles/36747/Quick-and-Dirty-HexDump-of-a-Byte-Array
    // https://blogs.msdn.microsoft.com/ericwhite/2010/03/12/hex-dump-using-linq-in-7-lines-of-code/

    public static partial class HexDumper
    {
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
            int idx = bytes.Length-1-length;
            int dlen = length;
            if(idx < 0)
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
            if (bytes == null) return "<null>";

            // compute default start index and byte range
            int startindex = index ?? 0;
            int bytesLength = length ?? bytes.Length;

            // Adjust bytes to process based on start index and requested length.
            // If size of input is less then startindex + requested range, adjust it so data till end is
            // displayed from start.
            bytesLength = (bytes.Length < (startindex + bytesLength)) ? bytes.Length - startindex : bytesLength;

            // Validate if we are not requesting bytes beyond input array size
            if (bytesLength <= 0) return "<No Data Reqested or Index/Length out of input byte[] range>";

            string[] results = new string[((int)(bytesLength / bytesPerLine)) + 1];
            int idx = 0;

            for (int i = startindex; i < bytesLength;)
            {
                int btd = (bytesPerLine <= (bytesLength - i)) ? bytesPerLine : (bytesLength - i);
                int fill = bytesPerLine - btd;

                // Convert a line of data into hex, replace "-" (as BitConverter generates)  with a space ' ' and pad
                // if data to be dumped is shorter then # of bytes per line.
                string hex = BitConverter.ToString(bytes, i, btd).Replace("-", " ").PadRight((bytesPerLine * 3) - 1);

                if (doTextDump)
                {
                    string text = "";

                    for (int j = i; j < btd + i; j++)
                    {
                        // Dump only "printable" characters. Range 0x20 to 0x7E inclusive 
                        // All else is replaced with a '.'
                        if ((byte)bytes[j] > (byte)0x1F && (byte)bytes[j] < (byte)0x7F)
                        {
                            text += System.Convert.ToChar(bytes[j]) + " ";
                        }
                        else
                        {
                            text += ". ";
                        }

                    }

                    results[idx++] = i.ToString("X6") + ": " + hex + " | " + text;
                }
                else
                {
                    results[idx++] = i.ToString("X6") + ": " + hex;
                }

                i += btd;
            }

            return String.Join("\n", results);
        }
    }
}

