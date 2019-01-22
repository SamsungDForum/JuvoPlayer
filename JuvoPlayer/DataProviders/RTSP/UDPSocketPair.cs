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

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using JuvoLogger;
using Rtsp;

namespace JuvoPlayer.DataProviders.RTSP
{
    internal class UDPSocketPair
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly UdpClient dataSocket = null;
        private readonly UdpClient controlSocket = null;

        private Thread dataReadThread = null;
        private Thread controlReadThread = null;

        public readonly int DataPort;
        public readonly int ControlPort;

        readonly IPAddress dataMulticastAddr;
        readonly IPAddress controlMulticastAddr;

        public bool IsMulticast { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UDPSocketPair"/> class.
        /// Creates two new UDP sockets using the start and end Port range
        /// </summary>
        public UDPSocketPair(int startPort, int endPort)
        {
            IsMulticast = false;

            // open a pair of UDP sockets - one for data (video or audio) and one for the status channel (RTCP messages)
            DataPort = startPort;
            ControlPort = startPort + 1;

            bool ok = false;
            while (ok == false && (ControlPort < endPort))
            {
                // Video/Audio port must be odd and command even (next one)
                try
                {
                    dataSocket = new UdpClient(DataPort);
                    controlSocket = new UdpClient(ControlPort);
                    ok = true;
                }
                catch (SocketException)
                {
                    // Fail to allocate port, try again
                    dataSocket?.Dispose();
                    controlSocket?.Dispose();

                    // try next data or control port
                    DataPort += 2;
                    ControlPort += 2;
                }
            }

            dataSocket.Client.ReceiveBufferSize = 100 * 1024;

            controlSocket.Client.DontFragment = false;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="UDPSocketPair"/> class.
        /// Used with Multicast mode with the Multicast Address and Port
        /// </summary>
        public UDPSocketPair(String dataMulticastAddress, int dataMulticastPort, String controlMulticastAddress, int controlMulticastPort)
        {
            IsMulticast = true;

            // open a pair of UDP sockets - one for data (video or audio) and one for the status channel (RTCP messages)
            this.DataPort = dataMulticastPort;
            this.ControlPort = controlMulticastPort;

            try
            {
                IPEndPoint dataEndPoint = new IPEndPoint(IPAddress.Any, DataPort);
                IPEndPoint controlEndPoint = new IPEndPoint(IPAddress.Any, ControlPort);

                dataMulticastAddr = IPAddress.Parse(dataMulticastAddress);
                controlMulticastAddr = IPAddress.Parse(controlMulticastAddress);

                dataSocket = new UdpClient();
                dataSocket.Client.Bind(dataEndPoint);
                dataSocket.JoinMulticastGroup(dataMulticastAddr);

                controlSocket = new UdpClient();
                controlSocket.Client.Bind(controlEndPoint);
                controlSocket.JoinMulticastGroup(controlMulticastAddr);

                dataSocket.Client.ReceiveBufferSize = 100 * 1024;

                controlSocket.Client.DontFragment = false;

            }
            catch (SocketException)
            {
                // Fail to allocate port, try again
                dataSocket?.Dispose();
                controlSocket?.Dispose();

                return;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            if (dataSocket == null || controlSocket == null)
            {
                throw new InvalidOperationException("UDP Forwarder host was not initialized, can't continue");
            }

            if (dataReadThread != null)
            {
                throw new InvalidOperationException("Forwarder was stopped, can't restart it");
            }

            dataReadThread = new Thread(() => DoWorkerJob(dataSocket, DataPort));
            dataReadThread.Name = "DataPort " + DataPort;
            dataReadThread.Start();

            controlReadThread = new Thread(() => DoWorkerJob(controlSocket, ControlPort));
            controlReadThread.Name = "ControlPort " + ControlPort;
            controlReadThread.Start();
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            if (IsMulticast)
            {
                // leave the multicast groups
                dataSocket.DropMulticastGroup(dataMulticastAddr);
                controlSocket.DropMulticastGroup(controlMulticastAddr);
            }

            dataSocket.Dispose();
            controlSocket.Dispose();
        }

        /// <summary>
        /// Occurs when message is received.
        /// </summary>
        public event EventHandler<Rtsp.RtspChunkEventArgs> DataReceived;

        /// <summary>
        /// Raises the <see cref="E:DataReceived"/> event.
        /// </summary>
        /// <param name="rtspChunkEventArgs">The <see cref="Rtsp.RtspChunkEventArgs"/> instance containing the event data.</param>
        protected void OnDataReceived(RtspChunkEventArgs rtspChunkEventArgs)
        {
            DataReceived?.Invoke(this, rtspChunkEventArgs);
        }

        /// <summary>
        /// Does the video job.
        /// </summary>
        private void DoWorkerJob(UdpClient socket, int dataPort)
        {
            EndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, dataPort);
            try
            {
                // loop until we get an exception eg the socket closed
                while (true)
                {
                    byte[] frame = new byte[dataSocket.Client.ReceiveBufferSize];
                    dataSocket.Client.ReceiveFrom(frame, ref ipEndPoint);

                    // We have an RTP frame.
                    // Fire the DataReceived event with 'frame'
                    Logger.Info("Received RTP data on port " + dataPort);

                    var currentMessage = new Rtsp.Messages.RtspData
                    {
                        Data = frame,
                        Channel = dataPort
                    };

                    OnDataReceived(new RtspChunkEventArgs(currentMessage));
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }
}
