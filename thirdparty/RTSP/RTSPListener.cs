using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using JuvoLogger;
using System.Reactive.Subjects;

namespace Rtsp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using Rtsp.Messages;

    /// <summary>
    /// Rtsp lister
    /// </summary>
    public class RtspListener : IDisposable
    {
        private static readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private IRtspTransport _transport;

        private Task _listenTask;
        private Stream _stream;

        private int _sequenceNumber;

        private Dictionary<int, RtspRequest> _sentMessage = new Dictionary<int, RtspRequest>();
        private Subject<string> _rtspErrorSubject;


        /// <summary>
        /// Initializes a new instance of the <see cref="RtspListener"/> class from a TCP connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public RtspListener(IRtspTransport connection, Subject<string> rtspErrorSubject)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");
            Contract.EndContractBlock();

            _transport = connection;
            _rtspErrorSubject = rtspErrorSubject;
        }

        /// <summary>
        /// Gets the remote address.
        /// </summary>
        /// <value>The remote adress.</value>
        public string RemoteAdress
        {
            get
            {
                return _transport.RemoteAddress;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start(CancellationToken token)
        {
            _stream = _transport.GetStream();

            _listenTask = new Task((o) => DoJob(token), token);
            _listenTask.ContinueWith((task) =>
            {
                if (task.Exception != null && !((CancellationToken)task.AsyncState).IsCancellationRequested)
                {
                    _rtspErrorSubject?.OnNext(task.Exception.Message);
                }
                _logger.Info("RTPS Listen Task completed.");
            });
            _listenTask.Start();
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            // brutally  close the TCP socket....
            // I hope the teardown was sent elsewhere
            _transport.Close();

        }

        /// <summary>
        /// Enable auto reconnect.
        /// </summary>
        public bool AutoReconnect { get; set; }

        /// <summary>
        /// Occurs when message is received.
        /// </summary>
        public event EventHandler<RtspChunkEventArgs> MessageReceived;

        /// <summary>
        /// Raises the <see cref="E:MessageReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="Rtsp.RtspChunkEventArgs"/> instance containing the event data.</param>
        protected void OnMessageReceived(RtspChunkEventArgs e)
        {
            EventHandler<RtspChunkEventArgs> handler = MessageReceived;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Occurs when Data is received.
        /// </summary>
        public event EventHandler<RtspChunkEventArgs> DataReceived;

        /// <summary>
        /// Raises the <see cref="E:DataReceived"/> event.
        /// </summary>
        /// <param name="rtspChunkEventArgs">The <see cref="Rtsp.RtspChunkEventArgs"/> instance containing the event data.</param>
        protected void OnDataReceived(RtspChunkEventArgs rtspChunkEventArgs)
        {
            EventHandler<RtspChunkEventArgs> handler = DataReceived;

            if (handler != null)
                handler(this, rtspChunkEventArgs);
        }

        /// <summary>
        /// Does the reading job.
        /// </summary>
        /// <remarks>
        /// This method read one message from TCP connection.
        /// If it a response it add the associate question.
        /// The stopping is made by the closing of the TCP connection.
        /// </remarks>
        private void DoJob(CancellationToken token)
        {
            try
            {
                _logger.Info($"RTSP Connection with {_transport.RemoteAddress} started");

                // token & _transport determine object's status
                while (!token.IsCancellationRequested && _transport?.Connected == true)
                {
                    // La lectuer est blocking sauf si la connection est coupé
                    RtspChunk currentMessage = ReadOneMessage();

                    if (currentMessage != null)
                    {
                        if (!(currentMessage is RtspData))
                        {
                            // on logue le tout
                            if (currentMessage.SourcePort != null)
                                _logger.Info($"Receive from {currentMessage.SourcePort.RemoteAdress}");
                            currentMessage.LogMessage();
                        }

                        if (currentMessage is RtspResponse)
                        {

                            RtspResponse response = currentMessage as RtspResponse;
                            lock (_sentMessage)
                            {
                                // add the original question to the response.
                                RtspRequest originalRequest;
                                if (_sentMessage.TryGetValue(response.CSeq, out originalRequest))
                                {
                                    _sentMessage.Remove(response.CSeq);
                                    response.OriginalRequest = originalRequest;
                                }
                                else
                                {
                                    _logger.Warn($"Receive response not asked {response.CSeq}");
                                }
                            }

                            OnMessageReceived(new RtspChunkEventArgs(response));
                        }
                        else if (currentMessage is RtspRequest)
                        {
                            OnMessageReceived(new RtspChunkEventArgs(currentMessage));
                        }
                        else if (currentMessage is RtspData)
                        {
                            OnDataReceived(new RtspChunkEventArgs(currentMessage));
                        }
                    }
                    else
                    {
                        _stream.Close();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Operation canceled");
                _stream.Close();
            }
            // Don't report IO/Socket errors when canceled.
            // May occur as a result of connection termination
            catch (IOException error)
            when (!token.IsCancellationRequested)
            {
                _logger.Error("IO Error" + error);
                _stream.Close();
                throw;
            }
            catch (SocketException error)
            when (!token.IsCancellationRequested)
            {
                _logger.Error("Socket Error" + error);
                _stream.Close();
                throw;
            }
            catch (ObjectDisposedException error)
            {
                _logger.Error("Object Disposed" + error);
                throw;
            }
            catch (Exception error)
            {
                _logger.Error("Unknown Error" + error);
                throw;
            }
            finally
            {
                _logger.Info($"RTSP Connection with {_transport.RemoteAddress} terminated");
            }
        }

        [Serializable]
        private enum ReadingState
        {
            NewCommand,
            Headers,
            Data,
            End,
            InterleavedData,
            MoreInterleavedData,
        }

        /// <summary>
        /// Sends the message.
        /// </summary>
        /// <param name="message">A message.</param>
        /// <returns><see cref="true"/> if it is Ok, otherwise <see cref="false"/></returns>
        public bool SendMessage(RtspMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            Contract.EndContractBlock();

            if (!_transport.Connected)
            {
                if (!AutoReconnect)
                    return false;

                _logger.Warn("Reconnect to a client, strange !!");
                try
                {
                    Reconnect();
                }
                catch (SocketException)
                {
                    // on a pas put se connecter on dit au manager de plus compter sur nous
                    return false;
                }
            }

            // if it it a request  we store the original message
            // and we renumber it.
            //TODO handle lost message (for example every minute cleanup old message)
            if (message is RtspRequest requestMsg)
            {
                // Original message has CSeq set. Make it so.
                message.CSeq = ++_sequenceNumber;
                RtspMessage originalMessage = message.Clone() as RtspMessage;
                ((RtspRequest)originalMessage).ContextData = requestMsg.ContextData;

                lock (_sentMessage)
                {
                    _sentMessage.Add(message.CSeq, originalMessage as RtspRequest);
                }
            }

            _logger.Info("Send Message");
            message.LogMessage();
            message.SendTo(_transport.GetStream());
            return true;
        }

        public Task Connect(string url)
        {
            return _transport.Connect(url);
        }

        /// <summary>
        /// Reconnect this instance of RtspListener.
        /// </summary>
        /// <exception cref="System.Net.Sockets.SocketException">Error during socket </exception>
        public void Reconnect()
        {
            //if it is already connected do not reconnect
            if (_transport.Connected)
                return;

            // If it is not connected listenthread should have die.
            if (_listenTask != null && !_listenTask.IsCompleted)
                _listenTask.Wait();

            _stream?.Dispose();

            // reconnect
            _transport.Reconnect();
            _stream = _transport.GetStream();

            // If listen thread exist restart it
            if (_listenTask != null)
                Start((CancellationToken)_listenTask.AsyncState);
        }

        /// <summary>
        /// Reads one message.
        /// </summary>
        /// <param name="commandStream">The Rtsp stream reader</param>
        /// <returns>Message reader</returns>
        public RtspChunk ReadOneMessage()
        {
            var commandStream = _transport.GetStream();
            if (commandStream == null)
                throw new ArgumentNullException("commandStream");
            Contract.EndContractBlock();

            ReadingState currentReadingState = ReadingState.NewCommand;
            // current decode message , create a fake new to permit compile.
            RtspChunk currentMessage = null;

            int size = 0;
            int byteReaden = 0;
            List<byte> buffer = new List<byte>(256);
            string oneLine = String.Empty;
            while (currentReadingState != ReadingState.End)
            {

                // if the system is not reading binary data.
                if (currentReadingState != ReadingState.Data && currentReadingState != ReadingState.MoreInterleavedData)
                {
                    oneLine = String.Empty;
                    bool needMoreChar = true;
                    // I do not know to make readline blocking
                    while (needMoreChar)
                    {
                        int currentByte = commandStream.ReadByte();

                        switch (currentByte)
                        {
                            case -1:
                                // the read is blocking, so if we got -1 it is because the client close;
                                currentReadingState = ReadingState.End;
                                needMoreChar = false;
                                break;
                            case '\n':
                                oneLine = ASCIIEncoding.UTF8.GetString(buffer.ToArray());
                                buffer.Clear();
                                needMoreChar = false;
                                break;
                            case '\r':
                                // simply ignore this
                                break;
                            case '$': // if first caracter of packet is $ it is an interleaved data packet
                                if (currentReadingState == ReadingState.NewCommand && buffer.Count == 0)
                                {
                                    currentReadingState = ReadingState.InterleavedData;
                                    needMoreChar = false;
                                }
                                else
                                    goto default;
                                break;
                            default:
                                buffer.Add((byte)currentByte);
                                break;
                        }
                    }
                }

                switch (currentReadingState)
                {
                    case ReadingState.NewCommand:
                        currentMessage = RtspMessage.GetRtspMessage(oneLine);
                        currentReadingState = ReadingState.Headers;
                        break;
                    case ReadingState.Headers:
                        string line = oneLine;
                        if (string.IsNullOrEmpty(line))
                        {
                            currentReadingState = ReadingState.Data;
                            ((RtspMessage)currentMessage).InitialiseDataFromContentLength();
                        }
                        else
                        {
                            ((RtspMessage)currentMessage).AddHeader(line);
                        }
                        break;
                    case ReadingState.Data:
                        if (currentMessage.Data.Length > 0)
                        {
                            // Read the remaning data
                            int byteCount = commandStream.Read(currentMessage.Data, byteReaden,
                                                               currentMessage.Data.Length - byteReaden);
                            if (byteCount <= 0)
                            {
                                currentReadingState = ReadingState.End;
                                break;
                            }
                            byteReaden += byteCount;
                            _logger.Info($"Read {byteReaden} byte of data");
                        }
                        // if we haven't read all go there again else go to end.
                        if (byteReaden >= currentMessage.Data.Length)
                            currentReadingState = ReadingState.End;
                        break;
                    case ReadingState.InterleavedData:
                        currentMessage = new RtspData();
                        int channelByte = commandStream.ReadByte();
                        if (channelByte == -1)
                        {
                            currentReadingState = ReadingState.End;
                            break;
                        }
                        ((RtspData)currentMessage).Channel = channelByte;

                        int sizeByte1 = commandStream.ReadByte();
                        if (sizeByte1 == -1)
                        {
                            currentReadingState = ReadingState.End;
                            break;
                        }
                        int sizeByte2 = commandStream.ReadByte();
                        if (sizeByte2 == -1)
                        {
                            currentReadingState = ReadingState.End;
                            break;
                        }
                        size = (sizeByte1 << 8) + sizeByte2;
                        currentMessage.Data = new byte[size];
                        currentReadingState = ReadingState.MoreInterleavedData;
                        break;
                    case ReadingState.MoreInterleavedData:
                        // apparently non blocking
                        {
                            int byteCount = commandStream.Read(currentMessage.Data, byteReaden, size - byteReaden);
                            if (byteCount <= 0)
                            {
                                currentReadingState = ReadingState.End;
                                break;
                            }
                            byteReaden += byteCount;
                            if (byteReaden < size)
                                currentReadingState = ReadingState.MoreInterleavedData;
                            else
                                currentReadingState = ReadingState.End;
                            break;
                        }
                    default:
                        break;
                }
            }
            if (currentMessage != null)
                currentMessage.SourcePort = this;
            return currentMessage;
        }

        /// <summary>
        /// Begins the send data.
        /// </summary>
        /// <param name="aRtspData">A Rtsp data.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <param name="aState">A state.</param>
        public IAsyncResult BeginSendData(RtspData aRtspData, AsyncCallback asyncCallback, object state)
        {
            if (aRtspData == null)
                throw new ArgumentNullException("aRtspData");
            Contract.EndContractBlock();

            return BeginSendData(aRtspData.Channel, aRtspData.Data, asyncCallback, state);
        }

        /// <summary>
        /// Begins the send data.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <param name="aState">A state.</param>
        public IAsyncResult BeginSendData(int channel, byte[] frame, AsyncCallback asyncCallback, object state)
        {
            if (frame == null)
                throw new ArgumentNullException("frame");
            if (frame.Length > 0xFFFF)
                throw new ArgumentException("frame too large", "frame");
            Contract.EndContractBlock();

            if (!_transport.Connected)
            {
                if (!AutoReconnect)
                    return null; // cannot write when transport is disconnected

                _logger.Warn("Reconnect to a client, strange !!");
                Reconnect();
            }

            byte[] data = new byte[4 + frame.Length]; // add 4 bytes for the header
            data[0] = 36; // '$' character
            data[1] = (byte)channel;
            data[2] = (byte)((frame.Length & 0xFF00) >> 8);
            data[3] = (byte)((frame.Length & 0x00FF));
            System.Array.Copy(frame, 0, data, 4, frame.Length);
            return _stream.BeginWrite(data, 0, data.Length, asyncCallback, state);
        }

        /// <summary>
        /// Ends the send data.
        /// </summary>
        /// <param name="result">The result.</param>
        public void EndSendData(IAsyncResult result)
        {
            try
            {
                _stream.EndWrite(result);
            }
            catch (Exception e)
            {
                // Error, for example stream has already been Disposed
                _logger.Warn("Error during end send (can be ignored) " + e);
                result = null;
            }
        }

        /// <summary>
        /// Send data (Synchronous)
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="frame">The frame.</param>
        public void SendData(int channel, byte[] frame)
        {
            if (frame == null)
                throw new ArgumentNullException("frame");
            if (frame.Length > 0xFFFF)
                throw new ArgumentException("frame too large", "frame");
            Contract.EndContractBlock();

            if (!_transport.Connected)
            {
                if (!AutoReconnect)
                    throw new Exception("Connection is lost");

                _logger.Warn("Reconnect to a client, strange !!");
                Reconnect();
            }

            byte[] data = new byte[4 + frame.Length]; // add 4 bytes for the header
            data[0] = 36; // '$' character
            data[1] = (byte)channel;
            data[2] = (byte)((frame.Length & 0xFF00) >> 8);
            data[3] = (byte)((frame.Length & 0x00FF));
            System.Array.Copy(frame, 0, data, 4, frame.Length);
            lock (_stream)
            {
                _stream.Write(data, 0, data.Length);
            }
        }


        #region IDisposable Membres

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose "owned" elements only.                
                _stream?.Dispose();
                _stream = null;
                _transport = null;  // Not owned
            }
        }

        #endregion
    }
}
