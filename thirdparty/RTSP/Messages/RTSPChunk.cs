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
    /// Class wich represent each message echanged on Rtsp socket.
    /// </summary>
    public abstract class RtspChunk
    {
        /// <summary>
        /// Logs the message to debug.
        /// </summary>
        public void LogMessage()
        {
            //LogMessage(NLog.LogLevel.Debug);
        }

        ///// <summary>
        ///// Logs the message.
        ///// </summary>
        ///// <param name="alevel">The log level.</param>
        //public abstract void LogMessage(NLog.LogLevel aLevel);

        /// <summary>
        /// Gets or sets the data associate with the message.
        /// </summary>
        /// <value>Array of byte transmit with the message.</value>
        public byte[] Data
        { get; set; }

        /// <summary>
        /// Gets or sets the source port wich receive the message.
        /// </summary>
        /// <value>The source port.</value>
        public RtspListener SourcePort { get; set; }

        #region ICloneable Membres

        /// <summary>
        /// Crée un nouvel objet qui est une copie de l'instance en cours.
        /// </summary>
        /// <returns>
        /// Nouvel objet qui est une copie de cette instance.
        /// </returns>
        public abstract object Clone();
        
        #endregion
    }
}
