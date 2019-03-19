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
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Nito.AsyncEx;
using Rtsp.Messages;
using Rtsp.Sdp;

namespace JuvoPlayer.DataProviders.RTSP
{
    internal class RTSPClient : IRTSPClient
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        RTPTransportType rtpTransportType = RTPTransportType.TCP; // Mode, either RTP over UDP or RTP over TCP using the RTSP socket
        UDPSocketPair udpSocketPair; // Pair of UDP ports used in RTP over UDP mode or in MULTICAST mode
        Rtsp.RtspListener rtspListener;
        Rtsp.RtspTcpTransport rtspSocket;

        string rtspUrl = "";
        string rtspSession = "";
        int videoDataChannel = -1; // RTP Channel Number used for the video stream or the UDP port number
        int videoRTCPChannel = -1; // RTP Channel Number used for the rtcp status report messages OR the UDP port number
        int videoPayloadType = -1; // Payload Type for the Video. (often 96 which is the first dynamic payload value)

        Timer timer;
        AutoResetEvent timerResetEvent;

        private readonly Subject<byte[]> chunkReadySubject = new Subject<byte[]>();
        private readonly Subject<string> rtspErrorSubject = new Subject<string>();

        public bool IsStarted { get; private set; }

        public IObservable<byte[]> ChunkReady()
        {
            return chunkReadySubject.AsObservable();
        }

        public IObservable<string> RTSPError()
        {
            return rtspErrorSubject;
        }

        public void Pause()
        {
            RtspRequest pauseMessage = new RtspRequestPause
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            };

            rtspListener.SendMessage(pauseMessage);
        }

        public void Play()
        {
            RtspRequest playMessage = new RtspRequestPlay
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            };

            rtspListener.SendMessage(playMessage);
        }

        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        public async Task Start(ClipDefinition clip, CancellationToken ctx)
        {
            try
            {
                IsStarted = false;
                await ExecuteStart(clip, ctx);
                IsStarted = true;
            }
            catch (TaskCanceledException)
            {
                rtspErrorSubject.OnNext("Connection timeout.");
            }
            catch (Exception e)
            {
                rtspErrorSubject.OnNext(e.Message);
            }
        }

        private async Task ExecuteStart(ClipDefinition clip, CancellationToken ctx)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null.");

            if (clip.Url.Length < 7)
                throw new ArgumentException("Clip URL cannot be empty.");

            rtspUrl = clip.Url;
            TcpClient tcpClient = new TcpClient();
            tcpClient.SendTimeout = 10000;
            tcpClient.ReceiveTimeout = 10000;
            try
            {
                Uri uri = new Uri(rtspUrl);
                await tcpClient.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 554).WaitAsync(ctx);
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                tcpClient.Dispose();
                throw;
            }

            rtspSocket = new Rtsp.RtspTcpTransport(tcpClient);
            if (rtspSocket.Connected == false)
                throw new Exception("RTSP server not available at this time.");

            rtspListener = new Rtsp.RtspListener(rtspSocket, rtspErrorSubject);
            rtspListener.MessageReceived += RtspMessageReceived;
            rtspListener.DataReceived += RtpDataReceived;
            rtspListener.AutoReconnect = false;
            rtspListener.Start();

            RtspRequest optionsMessage = new RtspRequestOptions
            {
                RtspUri = new Uri(rtspUrl)
            };

            rtspListener.SendMessage(optionsMessage);
        }

        public void Stop()
        {
            IsStarted = false;

            rtspListener?.SendMessage(new RtspRequestTeardown
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            });

            udpSocketPair?.Stop(); // clear up any UDP sockets
            timer?.Dispose(); // Stop the keepalive timer
            rtspListener?.Stop(); // Drop the RTSP session
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

        private void HandleRtcpGoodbye()
        {
            chunkReadySubject.OnNext(null);
        }

        public void RtpDataReceived(object sender, Rtsp.RtspChunkEventArgs chunkEventArgs)
        {
            try
            {
                RtspData rtpData = chunkEventArgs.Message as RtspData;

                if (IsRtcpGoodbye(rtpData))
                    HandleRtcpGoodbye();

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
                Logger.Info(e.ToString());
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
            chunkReadySubject.OnNext(rtp_payload);
        }

        // RTSP Messages are OPTIONS, DESCRIBE, SETUP, PLAY etc
        private void RtspMessageReceived(object sender, Rtsp.RtspChunkEventArgs chunkEventArgs)
        {
            RtspResponse message = chunkEventArgs.Message as RtspResponse;
            if (message?.OriginalRequest == null)
                return;

            Logger.Info("Received " + message.OriginalRequest);

            try
            {
                if (message.OriginalRequest is RtspRequestOptions)
                {
                    ProcessOptionsRequest(message);
                }
                else if (message.OriginalRequest is RtspRequestDescribe)
                {
                    ProcessDescribeRequest(message);
                }
                else if (message.OriginalRequest is RtspRequestSetup)
                {
                    ProcessSetupRequest(message);
                }
                else if (message.OriginalRequest is RtspRequestPlay)
                {
                    ProcessPlayRequest(message);
                }
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                rtspErrorSubject.OnNext("RTSP error occured.");
            }
        }

        private static void ProcessPlayRequest(RtspResponse message)
        {
            // If we get a reply to PLAY (which was our fourth command), then we should have video being received
            Logger.Info("Got reply from Play  " + message.Command);
        }

        private void ProcessSetupRequest(RtspResponse message)
        {
            // If we get a reply to SETUP (which was our third command), then process then send PLAY

            Logger.Info("Got reply from Setup. Session is " + message.Session);

            rtspSession = message.Session; // Session value used with Play, Pause, Teardown

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

            rtspListener.SendMessage(new RtspRequestPlay
            {
                RtspUri = new Uri(rtspUrl),
                Session = rtspSession
            });
        }

        private void ProcessDescribeRequest(RtspResponse message)
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

                    rtspListener.SendMessage(setupMessage);
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

        private void ProcessOptionsRequest(RtspResponse message)
        {
            // If we get a reply to OPTIONS and CSEQ is 1 (which was our first command), then send the DESCRIBE
            // If we fer a reply to OPTIONS and CSEQ is not 1, it must have been a keepalive command
            if (message.CSeq != 1)
                return;

            // Start a Timer to send an OPTIONS command (for keepalive) every 20 seconds
            timerResetEvent = new AutoResetEvent(false);
            timer = new Timer(Timeout, timerResetEvent, 20 * 1000, 20 * 1000);

            // send the Describe
            RtspRequest describeMessage = new RtspRequestDescribe
            {
                RtspUri = new Uri(rtspUrl)
            };

            rtspListener.SendMessage(describeMessage);
        }

        void Timeout(object stateInfo)
        {
            // Send Keepalive message
            RtspRequest optionsMessage = new RtspRequestOptions
            {
                RtspUri = new Uri(rtspUrl)
            };

            try
            {
                if (rtspListener.SendMessage(optionsMessage) == false)
                    rtspErrorSubject.OnNext("Connection timeout.");
            }
            catch (Exception e)
            {
                Logger.Info(e.ToString());
                rtspErrorSubject.OnNext("Connection timeout.");
            }
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
                chunkReadySubject.OnNext(new byte[] { 0x00, 0x00, 0x00, 0x01 });  // Write Start Code
                chunkReadySubject.OnNext(nalUnit);           // Write NAL
            }
        }
    }
}