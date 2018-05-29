// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
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
using System.Net;
using System.IO;
using JuvoLogger;

namespace MpdParser.Network
{
    public class ByteRange
    {
        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        public long Low { get; }
        public long High { get; }

        public ByteRange(string range)
        {
            var ranges = range.Split('-');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Invalid range");
            }
            try
            {
                Low = long.Parse(ranges[0]);
                High = long.Parse(ranges[1]);

                if (Low > High)
                {
                    throw new ArgumentException("Range Low param cannot be higher than High param");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Cannot parse range \"{range}\": {ex}");
            }
        }

        public static ByteRange FromString(string range)
        {
            if (range == null)
            {
                return null;
            }
            return new ByteRange(range);
        }

        public override string ToString() { return $"{Low}-{High}"; }
    }

    public class Downloader
    {
        public static byte[] DownloadData(Uri address, ByteRange range = null)
        {
            var request = HttpWebRequest.CreateDefault(address) as HttpWebRequest;

            request.AllowAutoRedirect = true;
            request.Timeout = _timeoutMs;
            if (range != null)
            {
                request.AddRange(range.Low, range.High);
            }

            var response = request.GetResponse() as HttpWebResponse;
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            {
                throw new WebException($"{address} [{range}] returned HTTP {response.StatusCode}");
            }

            var len = Convert.ToInt32(response.Headers["Content-Length"]);
            using (Stream stream = response.GetResponseStream(), mem = new MemoryStream(len != 0 ? len : avgDownloadSize))
            {
                stream.CopyTo(mem);
                return ((MemoryStream) mem).ToArray();
            }
        }

        //seems like a good default that won't drop data on slow-ish connections, yet not frustrate the user with wait times
        private static int _timeoutMs = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
        private const Int32 avgDownloadSize = 1024;
    }
}