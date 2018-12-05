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

namespace Rtsp.Sdp
{
    public class Attribut
    {
        private static readonly Dictionary<string, Type> attributMap = new Dictionary<string, Type>()
        {
            {AttributRtpMap.NAME,typeof(AttributRtpMap)},
            {AttributFmtp.NAME,typeof(AttributFmtp)},
        };


        public virtual string Key { get; private set; }
        public virtual string Value { get; protected set; }

        public static void RegisterNewAttributeType(string key, Type attributType)
        {
            //if(!attributType.IsSubclassOf(typeof(Attribut)))
            //    throw new ArgumentException("Type must be subclass of Rtsp.Sdp.Attribut","attributType");

            attributMap[key] = attributType;
        }

        

        public Attribut()
        {
        }

        public Attribut(string key)
        {
            Key = key;
        }


        public static Attribut ParseInvariant(string value)
        {
            if(value == null)
                throw new ArgumentNullException("value");

            //Contract.EndContractBlock();

            var listValues = value.Split(new char[] {':'}, 2);
            

            Attribut returnValue;

            // Call parser of child type
            Type childType;
            attributMap.TryGetValue(listValues[0], out childType);
            if (childType != null)
            {
                if (listValues[0] == AttributRtpMap.NAME)
                {
                    returnValue = new AttributRtpMap();
                }
                else if (listValues[0] == AttributFmtp.NAME)
                {
                    returnValue = new AttributFmtp();
                }
                else
                {
                    returnValue = new Attribut(listValues[0]);
                }
            }
            else
            {
                returnValue = new Attribut(listValues[0]);
            }
            // Parse the value. Note most attributes have a value but recvonly does not have a value
            if (listValues.Count() > 1) returnValue.ParseValue(listValues[1]);

            return returnValue;
        }

        protected virtual void ParseValue(string value)
        {
            Value = value;
        }

        
    }
}
