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

namespace Rtsp.Sdp
{
    /// <summary>
    /// Object ot represent orgin in an Session Description Protocol 
    /// </summary>
    public class Origin
    {
        public Origin()
        {
        }

        /// <summary>
        /// Parses the specified origin string.
        /// </summary>
        /// <param name="originString">The string to convert to origin object.</param>
        /// <returns></returns>
        public static Origin Parse(string originString)
        {
            if (originString == null)
                throw new ArgumentNullException("originString");

            string[] parts = originString.Split(' ');

            if (parts.Length != 6)
                throw new FormatException("Number of element invalid in origin string.");

            Origin result = new Origin();
            result.Username = parts[0];
            result.SessionId = parts[1];
            result.SessionVersion = long.Parse(parts[2]);
            result.NetType = parts[3];
            result.AddressType = parts[4];
            result.UnicastAddress = parts[5];

            return result;
        }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        /// <remarks>It is the user's login on the originating host, or it is "-"
        /// if the originating host does not support the concept of user IDs.
        /// This MUST NOT contain spaces</remarks>
        /// <value>The username.</value>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the session id.
        /// </summary>
        /// <remarks>It is a numeric string such that the tuple of <see cref="Username"/>,
        /// <see cref="SessionId"/>, <see cref="NetType"/>, <see cref="AddressType"/>, and <see cref="UnicastAddress"/> forms a
        /// globally unique identifier for the session.  The method of
        /// <see cref="SessionId"/> allocation is up to the creating tool, but it has been
        /// suggested that a Network Time Protocol (NTP) format timestamp be
        /// used to ensure uniqueness</remarks>
        /// <value>The session id.</value>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the session version.
        /// </summary>
        /// <value>The session version.</value>
        public long SessionVersion { get; set; }

        /// <summary>
        /// Gets or sets the type of the net.
        /// </summary>
        /// <value>The type of the net.</value>
        public string NetType { get; set; }

        /// <see cref="SessionId"/><summary>
        /// Gets or sets the type of the address.
        /// </summary>
        /// <value>The type of the address.</value>
        public string AddressType { get; set; }

        /// <summary>
        /// Gets or sets the unicast address (IP or FDQN).
        /// </summary>
        /// <value>The unicast address.</value>
        public string UnicastAddress { get; set; }

        public override string ToString()
        {
            return String.Join(" ",
                new string[]
                {
                    Username,
                    SessionId,
                    SessionVersion.ToString(CultureInfo.InvariantCulture),
                    NetType,
                    AddressType,
                    UnicastAddress,
                }
                );
        }
    }
}
