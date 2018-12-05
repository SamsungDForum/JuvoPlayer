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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Net;

namespace Rtsp.Sdp
{
    public abstract class Connection
    {
        public Connection()
        {
            //Default value from spec
            NumberOfAddress = 1;
        }

        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the number of address specifed in connection.
        /// </summary>
        /// <value>The number of address.</value>
        //TODO handle it a different way (list of adress ?)
        public int NumberOfAddress { get; set; }

        public static Connection Parse(string value)
        {
            if(value ==null)
                throw new ArgumentNullException("value");

            string[] parts = value.Split(' ');

            if (parts.Length != 3)
                throw new FormatException("Value do not contain 3 parts as needed.");

            if (parts[0] != "IN")
                throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Net type {0} not suported", parts[0]));

            switch (parts[1])
            {
                case "IP4":
                    return ConnectionIP4.Parse(parts[2]);
                case "IP6":
                    return ConnectionIP6.Parse(parts[2]);
                default:
                    throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Address type {0} not suported", parts[1]));
            }
            
        }
    }
}
