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
using JuvoPlayer.Common.Logging;

//Yes.. inheriting from WebClient is a simpler approach then raping the queen of Web/HttpRequests
namespace MpdParser.Network
{
    internal class ByteRange
    {
        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        public long Low { get; }
        public long High { get; }
        public ByteRange(string range)
        {
            Low = 0;
            High = 0;
            var ranges = range.Split('-');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Range cannot be parsed.");
            }
            try
            {
                Low = long.Parse(ranges[0]);
                High = long.Parse(ranges[1]);
            }
            catch (Exception ex)
            {
                Logger.Error(GetType().Name, ex.ToString() + " Cannot parse range.");
            }
        }
    }
    public class NetClient : WebClient
    {
        private long? _from;
        private long? _to;


        public void SetRange(long from, long to)
        {
            _from = from;
            _to = to;
        }

        public void ClearRange()
        {
            _from = null;
            _to = null;
        }

        public ulong GetBytes(Uri address)
        {
            OpenRead(address.ToString());
            return Convert.ToUInt64(ResponseHeaders["Content-Length"]);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            if (_to != null && _from != null)
            {
                request?.AddRange((int)_from, (int)_to);
            }
            return request;
        }
    }
}