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
using JuvoPlayer.Common;
using JuvoLogger;
using Rtsp.Messages;

namespace JuvoPlayer.DataProviders.RTSP
{
    internal class RTSPClient : IRTSPClient
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        RTPTransportType rtpTransportType = RTPTransportType.TCP; // Mode, either RTP over UDP or RTP over TCP using the RTSP socket
        UDPSocketPair udpPair = null;       // Pair of UDP ports used in RTP over UDP mode or in MULTICAST mode
        String url = "";                 // RTSP URL
        string session = "";             // RTSP Session
        int videoDataChannel = -1;     // RTP Channel Number used for the video stream or the UDP port number
        int videoRTCPChannel = -1;     // RTP Channel Number used for the rtcp status report messages OR the UDP port number

        Timer timer = null;
        AutoResetEvent timerResetEvent = null;

        Rtsp.RtspListener rtspListener = null;
        Rtsp.RtspTcpTransport rtspSocket = null; // RTSP connection

        int videoPayloadType = -1;          // Payload Type for the Video. (often 96 which is the first dynamic payload value)

        private readonly Subject<byte[]> chunkReadySubject = new Subject<byte[]>();

        public IObservable<byte[]> ChunkReady()
        {
            return chunkReadySubject.AsObservable();
        }

        public void Pause()
        {
            RtspRequest pauseMessage = new RtspRequestPause
            {
                RtspUri = new Uri(url),
                Session = session
            };

            rtspListener.SendMessage(pauseMessage);
        }

        public void Play()
        {
            RtspRequest playMessage = new RtspRequestPlay
            {
                RtspUri = new Uri(url),
                Session = session
            };

            rtspListener.SendMessage(playMessage);
        }

        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        public void Start(ClipDefinition clip)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip), "clip cannot be null");

            if (clip.Url.Length  < 7)
                throw new ArgumentException("clip url cannot be empty");

            url = clip.Url;

            Uri uri = new Uri(url);
            TcpClient tcpClient = new TcpClient();

            IAsyncResult asyncResult = tcpClient.BeginConnect(uri.Host, uri.Port > 0 ? uri.Port : 554, null, null);
            WaitHandle waitHandle = asyncResult.AsyncWaitHandle;
            try
            {
                if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2), false))
                {
                    tcpClient.Close();
                    throw new TimeoutException();
                }

                tcpClient.EndConnect(asyncResult);
            }
            finally
            {
                waitHandle.Close();
            }

            rtspSocket = new Rtsp.RtspTcpTransport(tcpClient);

            if (rtspSocket.Connected == false)
                throw new Exception("RTSP server not available at this time.");

            rtspListener = new Rtsp.RtspListener(rtspSocket);

            rtspListener.MessageReceived += RtspMessageReceived;
            rtspListener.DataReceived += RtpDataReceived;

            rtspListener.Start();

            RtspRequest optionsMessage = new RtspRequestOptions
            {
                RtspUri = new Uri(url)
            };

            rtspListener.SendMessage(optionsMessage);
        }

        public void Stop()
        {
            if (rtspListener != null)
            {
                RtspRequest teardownMessage = new RtspRequestTeardown
                {
                    RtspUri = new Uri(url),
                    Session = session
                };

                rtspListener.SendMessage(teardownMessage);
            }

            // clear up any UDP sockets
            udpPair?.Stop();

            // Stop the keepalive timer
            timer?.Dispose();

            // Drop the RTSP session
            rtspListener?.Stop();
        }

        public void RtpDataReceived(object sender, Rtsp.RtspChunkEventArgs e)
        {
            RtspData rtspData = e.Message as RtspData;

            // Check which channel the Data was received on.
            // eg the Video Channel, the Video Control Channel (RTCP)
            // In the future would also check the Audio Channel and Audio Control Channel
            if (rtspData.Channel == videoRTCPChannel)
            {
                Logger.Info("Received a RTCP message on channel " + rtspData.Channel);
                return;
            }

            if (rtspData.Channel == videoDataChannel)
                ProcessRTPVideo(e);
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
        private void RtspMessageReceived(object sender, Rtsp.RtspChunkEventArgs e)
        {
            RtspResponse message = e.Message as RtspResponse;
            if (message?.OriginalRequest == null)
                return;

            Logger.Info("Received " + message.OriginalRequest);

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

        private static void ProcessPlayRequest(RtspResponse message)
        {
            // If we get a reply to PLAY (which was our fourth command), then we should have video being received
            Logger.Info("Got reply from Play  " + message.Command);
        }

        private void ProcessSetupRequest(RtspResponse message)
        {
            // If we get a reply to SETUP (which was our third command), then process then send PLAY

            // Got Reply to SETUP
            Logger.Info("Got reply from Setup. Session is " + message.Session);

            session = message.Session; // Session value used with Play, Pause, Teardown

            // Check the Transport header
            if (message.Headers.ContainsKey(RtspHeaderNames.Transport))
            {
                RtspTransport transport = RtspTransport.Parse(message.Headers[RtspHeaderNames.Transport]);
                // Check if Transport header includes Multicast
                if (transport.IsMulticast)
                {
                    String multicastAddress = transport.Destination;
                    videoDataChannel = transport.Port.First;
                    videoRTCPChannel = transport.Port.Second;

                    // Create the Pair of UDP Sockets in Multicast mode
                    udpPair = new UDPSocketPair(multicastAddress, videoDataChannel, multicastAddress, videoRTCPChannel);
                    udpPair.DataReceived += RtpDataReceived;
                    udpPair.Start();
                }
            }

            RtspRequest play_message = new RtspRequestPlay
            {
                RtspUri = new Uri(url),
                Session = session
            };

            rtspListener.SendMessage(play_message);
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
                if (sdpData.Medias[x].GetMediaType() == Rtsp.Sdp.Media.MediaType.video)
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
                        OutputNAL(param.SpropParameterSets); // output SPS and PPS
                    }

                    // Split the rtpmap to get the Payload Type
                    videoPayloadType = 0;
                    if (rtpmap != null)
                        videoPayloadType = rtpmap.PayloadNumber;

                    RtspRequestSetup setupMessage = new RtspRequestSetup();
                    setupMessage.RtspUri = new Uri(url + "/" + control);

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
                    videoDataChannel = udpPair.DataPort;     // Used in DataReceived event handler
                    videoRTCPChannel = udpPair.ControlPort;  // Used in DataReceived event handler
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
            RtspRequest describe_message = new RtspRequestDescribe
            {
                RtspUri = new Uri(url)
            };

            rtspListener.SendMessage(describe_message);
        }

        void Timeout(Object stateInfo)
        {
            // Send Keepalive message
            RtspRequest options_message = new RtspRequestOptions
            {
                RtspUri = new Uri(url)
            };

            rtspListener.SendMessage(options_message);
        }

        // Output an array of NAL Units.
        // One frame of video may encoded in 1 large NAL unit, or it may be encoded in several small NAL units.
        // This function writes out all the NAL units that make one frame of video.
        // This is done to make it easier to feed H264 decoders which may require all the NAL units for a frame of video at the same time.

        // When writing to a .264 file we will add the Start Code 0x00 0x00 0x00 0x01 before each NAL unit
        // when outputting data for H264 decoders, please note that some decoders require a 32 bit size length header before each NAL unit instead of the Start Code
        private void OutputNAL(List<byte[]> nalUnits)
        {
            foreach (byte[] nalUnit in nalUnits)
            {
                chunkReadySubject.OnNext(new byte[] { 0x00, 0x00, 0x00, 0x01 });  // Write Start Code
                chunkReadySubject.OnNext(nalUnit);           // Write NAL
            }
        }
    }
}