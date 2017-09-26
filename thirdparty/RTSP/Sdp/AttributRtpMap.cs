using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rtsp.Sdp
{
    public class AttributRtpMap : Attribut
    {
        public const string NAME = "rtpmap";

        public AttributRtpMap()
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
                return string.Format("{0} {1}", PayloadNumber, Remaning);
            }
            protected set
            {
                ParseValue(value);
            }
        }

        public int PayloadNumber { get; set; }

        // temporary aatibute to store remaning data not parsed
        public string Remaning { get; set; }

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
                Remaning = parts[1];
            }


        }
    }
}
