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

namespace Rtsp.Sdp
{
    public class AttributFmtp : Attribut
    {
        public const string NAME = "fmtp";

        public AttributFmtp()
        {
        }

        public override string Key
        {
            get
            {
                return NAME;
            }
        }

        public override string Value
        {
            get
            {
                return string.Format("{0} {1}", PayloadNumber, FormatParameter);
            }
            protected set
            {
                ParseValue(value);
            }
        }

        public int PayloadNumber { get; set; }

        // temporary aatibute to store remaning data not parsed
        public string FormatParameter { get; set; }

        protected override void ParseValue(string value)
        {
            var parts = value.Split(new char[] { ' ' }, 2);

            int payloadNumber;
            if(int.TryParse(parts[0], out payloadNumber))
            {
                this.PayloadNumber = payloadNumber;
            }
            if(parts.Length > 1)
            {
                FormatParameter = parts[1];
            }


        }
    }
}
