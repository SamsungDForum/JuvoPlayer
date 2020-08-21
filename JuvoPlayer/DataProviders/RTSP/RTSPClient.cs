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
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Utils;
using Rtsp.Messages;
using Rtsp.Sdp;
using static Configuration.RTSPClient;
using System.Threading.Channels;
using Rtsp;

namespace JuvoPlayer.DataProviders.RTSP
{
    internal class RTSPClient : IRTSPClient
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private enum State { Idle, Starting, Playing, Paused, Terminating };
        private State _currentState = State.Idle;

        RTPTransportType rtpTransportType = RTPTransportType.TCP; // Mode, either RTP over UDP or RTP over TCP using the RTSP socket
        UDPSocketPair udpSocketPair; // Pair of UDP ports used in RTP over UDP mode or in MULTICAST mode
        Rtsp.RtspListener rtspListener;
        Rtsp.RtspTcpTransport rtspSocket;

        string rtspUrl = "";
        string rtspSession = null;
        int videoDataChannel = -1; // RTP Channel Number used for the video stream or the UDP port number
        int videoRTCPChannel = -1; // RTP Channel Number used for the rtcp status report messages OR the UDP port number
        int videoPayloadType = -1; // Payload Type for the Video. (often 96 which is the first dynamic payload value)

        private readonly Subject<byte[]> chunkReadySubject = new Subject<byte[]>();
        private readonly Subject<string> rtspErrorSubject = new Subject<string>();

        private Channel<RtspMessage> _rtspChannel;
        private Task _rtspTask = Task.CompletedTask;
        private CancellationTokenSource _rtpRtspCts;
        private bool _suspendTransfer = true;

        private static readonly TimeSpan PingPongTimeout = TimeSpan.FromSeconds(10);

        public RTSPClient()
        {
            _rtspChannel = Channel.CreateUnbounded<RtspMessage>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            });

            rtspSocket = new RtspTcpTransport();
            rtspListener = new RtspListener(rtspSocket, rtspErrorSubject);
            rtspListener.MessageReceived += (_, args) => _rtspChannel?.Writer.TryWrite(args.Message as RtspResponse);
            rtspListener.DataReceived += RtpDataReceived;
            rtspListener.AutoReconnect = false;
        }

        public IObservable<byte[]> ChunkReady()
        {
            return chunkReadySubject.AsObservable();
        }

        private void PushChunk(byte[] chunk)
        {
            chunkReadySubject.OnNext(chunk);
        }

        public IObservable<string> RTSPError()
        {
            return rtspErrorSubject;
        }

        public void Pause()
        {
            Logger.Info("");
            RequestPause();
        }

        private void RequestPause(object context = null)
        {
            PostRequest(new RtspRequestPause
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession,
                ContextData = context
            });
        }

        public void Play()
        {
            Logger.Info("");
            RequestPlay();
        }

        private void RequestPlay(object context = null)
        {
            PostRequest(new RtspRequestPlay
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession,
                ContextData = context
            });
        }

        public void SetDataClock(TimeSpan dataPosition)
        {
            // suspend transfer state change occurred.
            if (dataPosition == TimeSpan.Zero)
                RequestPause(true);
            else
                RequestPlay(true);
        }

        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        private void PostRequest(RtspRequest request)
        {
            try
            {
                if (!_rtspChannel.Writer.TryWrite(request))
                    Logger.Warn($"Posting {request} failed");
            }
            catch (ChannelClosedException)
            { }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        private async void Ping()
        {
            try
            {
                await Task.Delay(PingPongTimeout, _rtpRtspCts.Token);
                Logger.Info($"Ping {rtspListener.RemoteAdress}");
                RequestOptions();
            }
            catch (TaskCanceledException)
            {
                // ignore/
            }
        }

        private async Task RtspReader()
        {
            Logger.Info("RtspReader started");

            try
            {
                if (false == await Connect())
                    return;

                rtspListener.Start(_rtpRtspCts.Token);
                RequestOptions();

                while (!_rtpRtspCts.IsCancellationRequested && rtspSocket.Connected)
                {
                    var message = await _rtspChannel.Reader.ReadAsync(_rtpRtspCts.Token);

                    switch (message)
                    {
                        case RtspResponse response:
                            RtspMessageReceived(response);

                            // Exit if response is a result of teardown request.
                            if (response.OriginalRequest is RtspRequestTeardown)
                                return;

                            break;

                        case RtspRequest request:
                            RtspRequestReceived(request);
                            break;
                    }
                }
            }
            catch (Exception ex)
            when (!(ex is TaskCanceledException || ex is OperationCanceledException || ex is ChannelClosedException))
            {
                // Ignorable exceptions listed above
                Logger.Error(ex);
                rtspErrorSubject.OnNext($"Error. {ex.Message}");
            }
            finally
            {
                // Send EOS
                PushChunk(null);

                // Assures EOS is dispatched before termination
                await Task.Yield();
                ProcessTeardownResponse();

                Logger.Info("RtspReader stopped");
            }
        }

        private void RequestOptions()
        {
            Logger.Info("");

            PostRequest(new RtspRequestOptions
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            });
        }
        public Task Start(ClipDefinition clip)
        {
            if (_currentState != State.Idle)
                throw new InvalidOperationException($"{nameof(_currentState)} incorrect state {_currentState}");

            rtspUrl = clip.Url;
            _rtpRtspCts = new CancellationTokenSource();
            _rtspTask = Task.Run(async () => await RtspReader());

            return _rtspTask;
        }

        private async Task<bool> Connect()
        {
            try
            {
                if (rtspUrl == null)
                    throw new ArgumentNullException(nameof(rtspUrl), "Url cannot be null.");

                Logger.Info($"Connecting to {rtspUrl} with timeout {ConnectionTimeout}");

                await rtspListener.Connect(rtspUrl).WithTimeout(ConnectionTimeout);

                Logger.Info($"Connected to {rtspListener.RemoteAdress}");

                return true;
            }
            catch (OperationCanceledException)
            {

                var msg = $"Connect attempt {rtspUrl} timed out {ConnectionTimeout}";
                Logger.Error(msg);
                rtspErrorSubject.OnNext(msg);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                rtspErrorSubject.OnNext($"Connection error {e.Message}");
            }

            return false;
        }

        public Task Stop()
        {
            Logger.Info("");

            RequestStop();

            return _rtspTask;
        }

        private void RequestStop()
        {
            PostRequest(new RtspRequestTeardown
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            });
        }

        private bool IsRtcpGoodbye(RtspData rtspData)
        {
            int RTCPSenderReport = 200; // RTCP.PT=SR=200  ; RFC3550
            int RTCPGoodbye = 203;      // RTCP.PT=BYE=203 ; RFC3550

            int firstPacketType = rtspData.Data[1];
            if (firstPacketType != RTCPSenderReport)
                return false;

            int firstPacketLength = 4 * ((rtspData.Data[2] << 8) + rtspData.Data[3]) + 4;
            bool isThisACompoundPacket = rtspData.Data.Length > firstPacketLength;
            if (!isThisACompoundPacket)
                return false;

            int secondPacketType = rtspData.Data[firstPacketLength + 1];
            if (secondPacketType != RTCPGoodbye)
                return false;

            return true; // It's a RTCP Sender Report Goodbye compound packet, EoS.
        }

        private void ProcessTeardownResponse()
        {
            if (_rtpRtspCts == null || _rtpRtspCts.IsCancellationRequested == true)
                return;

            Logger.Info("");

            _rtpRtspCts.Cancel();

            if (_currentState != State.Idle)
            {
                rtspListener.Stop();
                udpSocketPair?.Stop();
                _currentState = State.Idle;
            }

            rtspSession = null;

            chunkReadySubject.OnCompleted();
            rtspErrorSubject.OnCompleted();
        }

        public void RtpDataReceived(object sender, RtspChunkEventArgs chunkEventArgs)
        {
            try
            {
                RtspData rtpData = chunkEventArgs.Message as RtspData;

                if (IsRtcpGoodbye(rtpData))
                {
                    ProcessTeardownResponse();
                    return;
                }

                // Check which channel the Data was received on.
                // eg the Video Channel, the Video Control Channel (RTCP)
                // In the future would also check the Audio Channel and Audio Control Channel
                if (rtpData.Channel == videoRTCPChannel)
                {
                    Logger.Info("Received a RTCP message on channel " + rtpData.Channel);
                    return;
                }

                if (rtpData.Channel == videoDataChannel)
                    ProcessRTPVideo(chunkEventArgs);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                rtspErrorSubject.OnNext(e.Message);
            }
        }

        private void ProcessRTPVideo(Rtsp.RtspChunkEventArgs e)
        {
            // Received some Video Data on the correct channel.

            // RTP Packet Header
            // 0 - Version, P, X, CC, M, PT and Sequence Number
            //32 - Timestamp
            //64 - SSRC
            //96 - CSRCs (optional)
            //nn - Extension ID and Length
            //nn - Extension header

            int rtpVersion = (e.Message.Data[0] >> 6);
            int rtpPadding = (e.Message.Data[0] >> 5) & 0x01;
            int rtpRxtension = (e.Message.Data[0] >> 4) & 0x01;
            int rtpCSRCCount = (e.Message.Data[0] >> 0) & 0x0F;
            int rtpMarker = (e.Message.Data[1] >> 7) & 0x01;
            int rtpPayloadType = (e.Message.Data[1] >> 0) & 0x7F;
            uint rtpSequenceNumber = ((uint)e.Message.Data[2] << 8) + (uint)(e.Message.Data[3]);
            uint rtpTimestamp = ((uint)e.Message.Data[4] << 24) + (uint)(e.Message.Data[5] << 16) + (uint)(e.Message.Data[6] << 8) + (uint)(e.Message.Data[7]);
            uint rtpSSRC = ((uint)e.Message.Data[8] << 24) + (uint)(e.Message.Data[9] << 16) + (uint)(e.Message.Data[10] << 8) + (uint)(e.Message.Data[11]);

            int rtpPayloadStart = 4 // V,P,M,SEQ
                                + 4 // time stamp
                                + 4 // ssrc
                                + (4 * rtpCSRCCount); // zero or more csrcs

            if (rtpRxtension == 1)
            {
                uint rtpExtensionId = ((uint)e.Message.Data[rtpPayloadStart + 0] << 8) + (uint)(e.Message.Data[rtpPayloadStart + 1] << 0);
                uint rtpExtensionSize = ((uint)e.Message.Data[rtpPayloadStart + 2] << 8) + (uint)(e.Message.Data[rtpPayloadStart + 3] << 0) * 4; // units of extension_size is 4-bytes
                rtpPayloadStart += 4 + (int)rtpExtensionSize;  // extension header and extension payload
            }

            //Logger.Info("RTP Data"
            //                   + " V=" + rtpVersion
            //                   + " P=" + rtpPadding
            //                   + " X=" + rtpRxtension
            //                   + " CC=" + rtpCSRCCount
            //                   + " M=" + rtpMarker
            //                   + " PT=" + rtpPayloadType
            //                   + " Seq=" + rtpSequenceNumber
            //                   + " Time (MS)=" + rtpTimestamp / 90 // convert from 90kHZ clock to ms
            //                   + " SSRC=" + rtpSSRC
            //                   + " Size=" + e.Message.Data.Length);

            // Check the payload type in the RTP packet matches the Payload Type value from the SDP
            if (videoPayloadType > 0 && rtpPayloadType != videoPayloadType)
            {
                Logger.Info("Ignoring this RTP payload: " + rtpPayloadType);
                return; // ignore this data
            }

            byte[] rtp_payload = new byte[e.Message.Data.Length - rtpPayloadStart]; // payload with RTP header removed
            Array.Copy(e.Message.Data, rtpPayloadStart, rtp_payload, 0, rtp_payload.Length); // copy payload
            PushChunk(rtp_payload);
        }

        private void RtspRequestReceived(RtspRequest request)
        {
            switch (request)
            {

                case RtspRequestOptions _ when _currentState == State.Idle:
                    _currentState = State.Starting;
                    break;

                // Request/State combinations not requiring additional processing.
                case RtspRequestOptions _ when _currentState != State.Idle && _currentState != State.Terminating:
                case RtspRequestDescribe _ when _currentState != State.Idle && _currentState != State.Terminating:
                case RtspRequestSetup _ when _currentState != State.Idle && _currentState != State.Terminating:
                case RtspRequestPlay _ when _currentState == State.Paused && _suspendTransfer == false:
                case RtspRequestPause _ when _currentState == State.Playing && _suspendTransfer == false:
                    break;

                case RtspRequestPlay _ when _currentState == State.Starting:
                    // Play requested - cannot be fulfilled due to _suspendTransfer state. 
                    if (_suspendTransfer)
                        return;

                    break;

                case RtspRequestPlay _ when request.ContextData != null:
                    if (!_suspendTransfer)
                        return;

                    _suspendTransfer = false;

                    // Resume.
                    // If session exists, always resume playback.
                    if (rtspSession == null)
                        return;

                    Logger.Info($"Resuming session {rtspSession}");
                    break;

                case RtspRequestPause _ when request.ContextData != null:
                    if (_suspendTransfer)
                        return;

                    _suspendTransfer = true;

                    // Suspend. 
                    // Always issue pause regardless of state (if sessionId exists)
                    // Pause->Play transition might have been requested, but not confirmed by server.
                    if (rtspSession == null)
                        return;

                    Logger.Info($"Pausing session {rtspSession}");
                    break;

                case RtspRequestTeardown _ when _currentState != State.Idle && _currentState != State.Terminating:
                    _currentState = State.Terminating;

                    // RTSP termination request before playback foreplay is done.
                    if (rtspSession == null)
                        return;

                    break;

                default:
                    // All misplaced requests fall here
                    Logger.Warn($"Incorrect Request/State");
                    Logger.Warn($"Request: {request.GetType()}");
                    Logger.Warn($"\tState: {_currentState}");
                    Logger.Warn($"\tTransfer suspended: {_suspendTransfer}");
                    Logger.Warn($"\tRtsp Session: {rtspSession}");
                    return;
            }

            Logger.Info($"Sending {request}");
            if (rtspListener.SendMessage(request))
                return;

            Logger.Warn($"Send {request} failed");
            // Use "existing" model for now. Fails are dropped. 
            // Failed requests can be posted back to queue for re-processing. 
        }

        // RTSP Messages are OPTIONS, DESCRIBE, SETUP, PLAY etc
        private void RtspMessageReceived(RtspResponse message)
        {
            Logger.Info($"Response {message.OriginalRequest}");
            if (_rtpRtspCts.IsCancellationRequested)
            {
                // Ignore requests when canceled. There's a change of pong getting returned
                // during termination
                Logger.Info("Response ignored. Cancellation requested.");
                return;
            }
            if (message?.OriginalRequest == null)
                return;

            if (message.CSeq != message.OriginalRequest.CSeq)
            {
                Logger.Warn($"CSeq mismatch. Request {message.OriginalRequest.CSeq} Response {message.CSeq}");
                // Resending may be in order
                return;
            }
            if (!message.IsOk)
            {
                Logger.Warn($"Error response {message.ReturnCode} {message.ReturnMessage}");
                return;
            }

            try
            {
                if (message.OriginalRequest is RtspRequestOptions)
                {
                    switch (_currentState)
                    {
                        case State.Terminating:
                            break;

                        case State.Starting:
                        case State.Playing:
                        case State.Paused:
                            if (rtspSession == null)
                            {
                                ProcessOptionsResponse();
                            }
                            else
                            {
                                // Ping-Pong once playback is started.
                                Logger.Info($"Pong {rtspListener.RemoteAdress}");
                                Ping();
                            }
                            break;

                        default:
                            Logger.Warn($"Incorrect state {_currentState}");
                            break;
                    }

                }
                else if (message.OriginalRequest is RtspRequestDescribe)
                {
                    switch (_currentState)
                    {
                        case State.Terminating:
                            break;

                        case State.Starting:
                            ProcessDescribeResponse(message);
                            break;

                        default:
                            Logger.Warn($"Incorrect state {_currentState}");
                            break;
                    }
                }
                else if (message.OriginalRequest is RtspRequestSetup)
                {
                    switch (_currentState)
                    {
                        case State.Terminating:
                            break;

                        case State.Starting:
                            ProcessSetupResponse(message);
                            Ping();
                            break;

                        default:
                            Logger.Warn($"Incorrect state {_currentState}");
                            break;
                    }
                }
                else if (message.OriginalRequest is RtspRequestPlay)
                {
                    switch (_currentState)
                    {
                        case State.Terminating:
                            break;

                        case State.Starting:
                            _currentState = State.Playing;
                            break;

                        case State.Paused:
                            _currentState = State.Playing;
                            break;

                        default:
                            Logger.Warn($"Incorrect state {_currentState}");
                            break;
                    }

                }
                else if (message.OriginalRequest is RtspRequestPause)
                {
                    switch (_currentState)
                    {
                        case State.Terminating:
                            break;

                        case State.Playing:
                            _currentState = State.Paused;
                            break;

                        default:
                            Logger.Warn($"Incorrect state {_currentState}");
                            break;
                    }

                }
                else if (message.OriginalRequest is RtspRequestTeardown)
                {
                    switch (_currentState)
                    {
                        case State.Terminating:
                            ProcessTeardownResponse();
                            _currentState = State.Idle;
                            break;

                        default:
                            Logger.Warn($"Incorrect state {_currentState}");
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                rtspErrorSubject.OnNext($"RTSP error. {e.Message}");
            }
        }

        private void ProcessSetupResponse(RtspResponse message)
        {
            rtspSession = message.Session; // Session value used with Play, Pause, Teardown
            Logger.Info($"RTSP session: {rtspSession}");

            if (message.Headers.ContainsKey(RtspHeaderNames.Transport))
            {
                RtspTransport transport = RtspTransport.Parse(message.Headers[RtspHeaderNames.Transport]);

                if (transport.IsMulticast)
                {
                    string multicastAddress = transport.Destination;
                    videoDataChannel = transport.Port.First;
                    videoRTCPChannel = transport.Port.Second;

                    // Create the Pair of UDP Sockets in Multicast mode
                    udpSocketPair = new UDPSocketPair(multicastAddress, videoDataChannel, multicastAddress, videoRTCPChannel);
                    udpSocketPair.DataReceived += RtpDataReceived;
                    udpSocketPair.Start();
                }
            }

            if (_suspendTransfer)
            {
                // Transfer suspended. Set state to playing but do not issue play request.
                // Play request will be sent upon resume.
                _currentState = State.Playing;
                return;
            }
            _currentState = State.Paused;
            PostRequest(new RtspRequestPlay
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            });
        }

        private void ProcessDescribeResponse(RtspResponse message)
        {
            // Got a reply for DESCRIBE
            // Examine the SDP
            Logger.Info(System.Text.Encoding.UTF8.GetString(message.Data));

            Rtsp.Sdp.SdpFile sdpData;
            using (StreamReader sdpStream = new StreamReader(new MemoryStream(message.Data)))
            {
                sdpData = Rtsp.Sdp.SdpFile.Read(sdpStream);
            }

            // Process each 'Media' Attribute in the SDP (each sub-stream)
            // If the attribute is for Video, then carry out a SETUP and a PLAY
            // Only do this for the first Video attribute in case there is more than one in the SDP
            for (int x = 0; x < sdpData.Medias.Count; x++)
            {
                if (sdpData.Medias[x].MediaType == Media.MediaTypes.video)
                {
                    // We only want the first video sub-stream
                    if (videoPayloadType != -1)
                        return;

                    // search the attributes for control, fmtp and rtpmap
                    ParseAttributes(sdpData, x, out string control, out Rtsp.Sdp.AttributFmtp fmtp, out Rtsp.Sdp.AttributRtpMap rtpmap);

                    // Split the fmtp to get the sprop-parameter-sets which hold the SPS and PPS in base64
                    if (fmtp != null)
                    {
                        var param = Rtsp.Sdp.H264Parameters.Parse(fmtp.FormatParameter);
                        OutputNal(param.SpropParameterSets); // output SPS and PPS
                    }

                    // Split the rtpmap to get the Payload Type
                    videoPayloadType = 0;
                    if (rtpmap != null)
                        videoPayloadType = rtpmap.PayloadNumber;

                    RtspRequestSetup setupMessage = new RtspRequestSetup();
                    setupMessage.RtspUri = new Uri(rtspUrl + "/" + control);

                    var transport = GetRTSPTransport();
                    setupMessage.AddTransport(transport);

                    PostRequest(setupMessage);
                }
            }
        }

        private RtspTransport GetRTSPTransport()
        {
            switch (rtpTransportType)
            {
                case RTPTransportType.TCP:
                    {
                        // Server interleaves the RTP packets over the RTSP connection
                        // Example for TCP mode (RTP over RTSP)   Transport: RTP/AVP/TCP;interleaved=0-1
                        videoDataChannel = 0;  // Used in DataReceived event handler
                        videoRTCPChannel = 1;  // Used in DataReceived event handler
                        return new RtspTransport()
                        {
                            LowerTransport = RtspTransport.LowerTransportType.TCP,
                            Interleaved = new PortCouple(videoDataChannel, videoRTCPChannel), // Channel 0 for video. Channel 1 for RTCP status reports
                        };
                    }
                case RTPTransportType.UDP:
                    {
                        // Server sends the RTP packets to a Pair of UDP Ports (one for data, one for rtcp control messages)
                        // Example for UDP mode                   Transport: RTP/AVP;unicast;client_port=8000-8001
                        videoDataChannel = udpSocketPair.DataPort;     // Used in DataReceived event handler
                        videoRTCPChannel = udpSocketPair.ControlPort;  // Used in DataReceived event handler
                        return new RtspTransport()
                        {
                            LowerTransport = RtspTransport.LowerTransportType.UDP,
                            IsMulticast = false,
                            ClientPort = new PortCouple(videoDataChannel, videoRTCPChannel), // a Channel for video. a Channel for RTCP status reports
                        };
                    }
                case RTPTransportType.MULTICAST:
                    {
                        // Server sends the RTP packets to a Pair of UDP ports (one for data, one for rtcp control messages)
                        // using Multicast Address and Ports that are in the reply to the SETUP message
                        // Example for MULTICAST mode     Transport: RTP/AVP;multicast
                        videoDataChannel = 0; // we get this information in the SETUP message reply
                        videoRTCPChannel = 0; // we get this information in the SETUP message reply
                        return new RtspTransport()
                        {
                            LowerTransport = RtspTransport.LowerTransportType.UDP,
                            IsMulticast = true
                        };
                    }
                default:
                    throw new Exception();
            }
        }

        private static void ParseAttributes(Rtsp.Sdp.SdpFile sdpData, int x, out string control, out Rtsp.Sdp.AttributFmtp fmtp, out Rtsp.Sdp.AttributRtpMap rtpmap)
        {
            control = "";
            fmtp = null;
            rtpmap = null;
            foreach (Rtsp.Sdp.Attribut attrib in sdpData.Medias[x].Attributs)
            {
                if (attrib.Key.Equals("control")) control = attrib.Value;
                if (attrib.Key.Equals("fmtp")) fmtp = attrib as Rtsp.Sdp.AttributFmtp;
                if (attrib.Key.Equals("rtpmap")) rtpmap = attrib as Rtsp.Sdp.AttributRtpMap;
            }
        }

        private void ProcessOptionsResponse()
        {
            // send the Describe
            RtspRequest describeMessage = new RtspRequestDescribe
            {
                RtspUri = new Uri(rtspUrl)
            };

            PostRequest(describeMessage);
        }

        // Output an array of NAL Units.
        // One frame of video may encoded in 1 large NAL unit, or it may be encoded in several small NAL units.
        // This function writes out all the NAL units that make one frame of video.
        // This is done to make it easier to feed H264 decoders which may require all the NAL units for a frame of video at the same time.

        // When writing to a .264 file we will add the Start Code 0x00 0x00 0x00 0x01 before each NAL unit
        // when outputting data for H264 decoders, please note that some decoders require a 32 bit size length header before each NAL unit instead of the Start Code
        private void OutputNal(List<byte[]> nalUnits)
        {
            foreach (byte[] nalUnit in nalUnits)
            {
                PushChunk(new byte[] { 0x00, 0x00, 0x00, 0x01 });  // Write Start Code
                PushChunk(nalUnit);           // Write NAL
            }
        }

        public void Dispose()
        {
            _rtpRtspCts?.Cancel();
            _rtpRtspCts?.Dispose();
            _rtpRtspCts = null;

            rtspListener.Dispose();
            rtspSocket.Dispose();

            chunkReadySubject.Dispose();
            rtspErrorSubject.Dispose();
        }
    }
}
