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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Threading.Tasks;

namespace Rtsp
{
    /// <summary>
    /// TCP Connection for Rtsp
    /// </summary>
    public class RtspTcpTransport : IRtspTransport, IDisposable
    {
        private IPEndPoint _currentEndPoint;
        private TcpClient _RtspServerClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="RtspTcpTransport"/> class.
        /// </summary>
        /// <param name="tcpConnection">The underlying TCP connection.</param>
        public RtspTcpTransport(TcpClient tcpConnection)
        {
            if (tcpConnection == null)
                throw new ArgumentNullException("tcpConnection");

            _currentEndPoint = (IPEndPoint)tcpConnection.Client.RemoteEndPoint;
            _RtspServerClient = tcpConnection;
        }

        ///// <summary>
        ///// Initializes a new instance of the <see cref="RtspTcpTransport"/> class.
        ///// </summary>
        ///// <param name="aHost">A host.</param>
        ///// <param name="aPortNumber">A port number.</param>
        //public RtspTcpTransport(string aHost, int aPortNumber)
        //    : this(new TcpClient(aHost, aPortNumber))
        //{
        //}


        #region IRtspTransport Membres

        /// <summary>
        /// Gets the stream of the transport.
        /// </summary>
        /// <returns>A stream</returns>
        public Stream GetStream()
        {
            return _RtspServerClient.GetStream();
        }

        /// <summary>
        /// Gets the remote address.
        /// </summary>
        /// <value>The remote address.</value>
        public string RemoteAddress
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture,"{0}:{1}", _currentEndPoint.Address, _currentEndPoint.Port);
            }
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="IRtspTransport"/> is connected.
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        public bool Connected
        {
            get { return _RtspServerClient.Client != null && _RtspServerClient.Connected; }
        }

        /// <summary>
        /// Reconnect this instance.
        /// <remarks>Must do nothing if already connected.</remarks>
        /// </summary>
        /// <exception cref="System.Net.Sockets.SocketException">Error during socket </exception>
        public void Reconnect()
        {
            if (Connected)
                return;
            _RtspServerClient = new TcpClient();
            var task = Task.Run(async () => { await _RtspServerClient.ConnectAsync(_currentEndPoint.Address, _currentEndPoint.Port); });
            task.Wait();
        }

        #endregion

        public void Dispose()
        {
            _RtspServerClient.Dispose();
        }
    }
}
