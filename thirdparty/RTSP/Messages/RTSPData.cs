using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JuvoLogger;

namespace Rtsp.Messages
{
    /// <summary>
    /// Message wich represent data. ($ limited message)
    /// </summary>
    public class RtspData : RtspChunk
    {
        //private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");


        /// <summary>
        /// Logs the message to debug.
        /// </summary>
        public override void LogMessage()
        {
            _logger.Info("Data message");
            if (Data == null)
                _logger.Info("Data : null");
            else
                _logger.Info($"Data length :-{Data.Length}-");
        }

        public int Channel { get; set; }

        /// <summary>
        /// Clones this instance.
        /// <remarks>Listner is not cloned</remarks>
        /// </summary>
        /// <returns>a clone of this instance</returns>
        public override object Clone()
        {
            RtspData result = new RtspData();
            result.Channel = this.Channel;
            if (this.Data != null)
                result.Data = this.Data.Clone() as byte[];
            result.SourcePort = this.SourcePort;
            return result;
        }
    }
}
