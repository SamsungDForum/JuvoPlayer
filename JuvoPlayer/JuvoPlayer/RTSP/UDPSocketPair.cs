using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Tizen;

using Rtsp;

namespace JuvoPlayer.RTSP
{
    public class UDPSocketPair
    {
        private UdpClient dataSocket = null;
        private UdpClient controlSocket = null;

        private Thread dataReadThread = null;
        private Thread controlReadThread = null;

        public int dataPort = 50000;
        public int controlPort = 50001;

        IPAddress dataMulticastAddr;
        IPAddress controlMulticastAddr;

        public bool IsMulticast { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UDPSocketPair"/> class.
        /// Creates two new UDP sockets using the start and end Port range
        /// </summary>
        public UDPSocketPair(int startPort, int endPort)
        {
            IsMulticast = false;

            // open a pair of UDP sockets - one for data (video or audio) and one for the status channel (RTCP messages)
            dataPort = startPort;
            controlPort = startPort + 1;

            bool ok = false;
            while (ok == false && (controlPort < endPort))
            {
                // Video/Audio port must be odd and command even (next one)
                try
                {
                    dataSocket = new UdpClient(dataPort);
                    controlSocket = new UdpClient(controlPort);
                    ok = true;
                }
                catch (SocketException)
                {
                    // Fail to allocate port, try again
                    if (dataSocket != null)
                        dataSocket.Dispose();
                    if (controlSocket != null)
                        controlSocket.Dispose();

                    // try next data or control port
                    dataPort += 2;
                    controlPort += 2;
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
            this.dataPort = dataMulticastPort;
            this.controlPort = controlMulticastPort;

            try
            {
                IPEndPoint dataEndPoint = new IPEndPoint(IPAddress.Any, dataPort);
                IPEndPoint controlEndPoint = new IPEndPoint(IPAddress.Any, controlPort);

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
                if (dataSocket != null)
                    dataSocket.Dispose();
                if (controlSocket != null)
                    controlSocket.Dispose();

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
                throw new InvalidOperationException("UDP Forwader host was not initialized, can't continue");
            }

            if (dataReadThread != null)
            {
                throw new InvalidOperationException("Forwarder was stopped, can't restart it");
            }

            dataReadThread = new Thread(() => DoWorkerJob(dataSocket, dataPort));
            dataReadThread.Name = "DataPort " + dataPort;
            dataReadThread.Start();

            controlReadThread = new Thread(() => DoWorkerJob(controlSocket, controlPort));
            controlReadThread.Name = "ControlPort " + controlPort;
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
                    Tizen.Log.Info("JuvoPlayer", "Received RTP data on port " + dataPort);

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
