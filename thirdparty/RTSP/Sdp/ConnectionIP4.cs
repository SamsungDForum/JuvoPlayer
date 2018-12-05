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

﻿namespace Rtsp.Sdp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    public class ConnectionIP4 : Connection
    {

        public int Ttl { get; set; }

        internal new static ConnectionIP4 Parse(string ipAddress)
        {
            string[] parts = ipAddress.Split('/');

            if (parts.Length > 3)
                throw new FormatException("Too much address subpart in " + ipAddress);

            ConnectionIP4 result = new ConnectionIP4();

            result.Host = parts[0];

            int ttl;
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ttl))
                    throw new FormatException("Invalid TTL format : " + parts[1]);
                result.Ttl = ttl;
            }
            int numberOfAddress;
            if (parts.Length > 2)
            {
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out numberOfAddress))
                    throw new FormatException("Invalid number of address : " + parts[2]);
                result.NumberOfAddress = numberOfAddress;
            }

            return result;
        }
    }
}
