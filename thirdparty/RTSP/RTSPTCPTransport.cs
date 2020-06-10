using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Threading.Tasks;

namespace Rtsp
{
    /// <summary>
    /// TCP Connection for Rtsp
    /// </summary>
    public class RtspTcpTransport : IRtspTransport, IDisposable
    {
        private TcpClient _RtspServerClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="RtspTcpTransport"/> class.
        /// </summary>
        /// <param name="tcpConnection">The underlying TCP connection.</param>
        public RtspTcpTransport(TcpClient tcpConnection)
        {
            if (tcpConnection == null)
                throw new ArgumentNullException("tcpConnection");
            Contract.EndContractBlock();

            _RtspServerClient = tcpConnection;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RtspTcpTransport"/> class.
        /// </summary>
        /// <param name="aHost">A host.</param>
        /// <param name="aPortNumber">A port number.</param>
        public RtspTcpTransport(string aHost, int aPortNumber)
            : this(new TcpClient(aHost, aPortNumber))
        {
        }

        public RtspTcpTransport()
            : this(new TcpClient())
        {
        }

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
                return _RtspServerClient.Client.RemoteEndPoint?.ToString() ?? null;
            }
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public void Close()
        {
            Dispose(true);
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

            var oldClient = _RtspServerClient;

            // reconnecting without prior connection will throw
            _RtspServerClient = new TcpClient();
            _RtspServerClient.Connect((IPEndPoint)_RtspServerClient.Client.RemoteEndPoint);

            oldClient.Close();
            oldClient.Dispose();
        }

        public Task Connect(string url)
        {
            if (Connected)
                return Task.CompletedTask;

            Uri connectUrl = new Uri(url);
            return _RtspServerClient.ConnectAsync(connectUrl.Host, connectUrl.Port > 0 ? connectUrl.Port : 554);
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _RtspServerClient?.Dispose();
                _RtspServerClient = null;
            }
        }
    }
}
