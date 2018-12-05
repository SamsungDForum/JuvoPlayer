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

namespace Rtsp.Messages
{
    /// <summary>
    /// Message wich represent data. ($ limited message)
    /// </summary>
    public class RtspData : RtspChunk
    {
        //private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Logs the message to debug.
        /// </summary>
        //public override void LogMessage(NLog.LogLevel aLevel)
        //{
        //    // Default value to debug
        //    if (aLevel == null)
        //        aLevel = NLog.LogLevel.Debug;
        //    // if the level is not logged directly return
        //    if (!_logger.IsEnabled(aLevel))
        //        return;
        //    _logger.Log(aLevel, "Data message");
        //    if (Data == null)
        //        _logger.Log(aLevel, "Data : null");
        //    else
        //        _logger.Log(aLevel, "Data length :-{0}-", Data.Length);
        //}

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
