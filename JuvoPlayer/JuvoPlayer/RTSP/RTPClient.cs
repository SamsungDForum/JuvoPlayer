// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using Rtsp.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace JuvoPlayer.RTSP
{
    public class RTPClient : IRTPClient
    {
        RTPTransportType rtpTransportType = RTPTransportType.TCP; // Mode, either RTP over UDP or RTP over TCP using the RTSP socket
        UDPSocketPair udpPair = null;       // Pair of UDP ports used in RTP over UDP mode or in MULTICAST mode
        String url = "";                 // RTSP URL
        string session = "";             // RTSP Session
        int videoDataChannel = -1;     // RTP Channel Number used for the video stream or the UDP port number
        int videoRTCPChannel = -1;     // RTP Channel Number used for the rtcp status report messages OR the UDP port number
        List<byte[]> temporary_rtp_payloads = new List<byte[]>(); // used to assemble the RTP packets that form one RTP frame
        MemoryStream fragmentedNAL = new MemoryStream(); // used to concatenate fragmented H264 NALs where NALs are split over RTP packets

        ISharedBuffer buffer;

        Timer timer = null;
        AutoResetEvent timerResetEvent = null;

        Rtsp.RtspListener rtspListener = null;
        Rtsp.RtspTcpTransport rtspSocket = null; // RTSP connection

        int videoPayloadType = -1;          // Payload Type for the Video. (often 96 which is the first dynamic payload value)

        public RTPClient(ISharedBuffer buffer)
        {
            this.buffer = buffer ?? throw new ArgumentNullException("buffer cannot be null");
        }
        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            try
            {
                TcpClient tcpClient = new TcpClient();
                tcpClient.ConnectAsync("127.0.0.1", 8080).RunSynchronously();
                var rtsp_socket = new Rtsp.RtspTcpTransport(tcpClient);
                if (rtsp_socket.Connected == false)
                {
                    Console.WriteLine("Error - did not connect");
                    return;
                }

                // Connect a RTSP Listener to the RTSP Socket (or other Stream) to send RTSP messages and listen for RTSP replies
                rtspListener = new Rtsp.RtspListener(rtsp_socket);

                rtspListener.MessageReceived += RtspMessageReceived;
                rtspListener.DataReceived += RtpDataReceived;

                rtspListener.Start();

                RtspRequest optionsMessage = new RtspRequestOptions
                {
                    RtspUri = new Uri(url)
                };

                rtspListener.SendMessage(optionsMessage);
            }
            catch
            {
                Tizen.Log.Info("JuvoPlayer", "Error - did not connect");
                return;
            }
        }

        public void Stop()
        {
            RtspRequest teardownMessage = new RtspRequestTeardown
            {
                RtspUri = new Uri(url),
                Session = session
            };

            rtspListener.SendMessage(teardownMessage);

            // clear up any UDP sockets
            if (udpPair != null)
                udpPair.Stop();

            // Stop the keepalive timer
            if (timer != null)
                timer.Dispose();

            // Drop the RTSP session
            rtspListener.Stop();
        }

        public void RtpDataReceived(object sender, Rtsp.RtspChunkEventArgs e)
        {
            RtspData rtpData = e.Message as RtspData;

            // Check which channel the Data was received on.
            // eg the Video Channel, the Video Control Channel (RTCP)
            // In the future would also check the Audio Channel and Audio Control Channel
            if (rtpData.Channel == videoRTCPChannel)
            {
                Tizen.Log.Info("JuvoPlayer", "Received a RTCP message on channel " + rtpData.Channel);
                return;
            }

            if (rtpData.Channel == videoDataChannel)
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

            Tizen.Log.Info("JuvoPlayer", "RTP Data"
                               + " V=" + rtpVersion
                               + " P=" + rtpPadding
                               + " X=" + rtpRxtension
                               + " CC=" + rtpCSRCCount
                               + " M=" + rtpMarker
                               + " PT=" + rtpPayloadType
                               + " Seq=" + rtpSequenceNumber
                               + " Time (MS)=" + rtpTimestamp / 90 // convert from 90kHZ clock to ms
                               + " SSRC=" + rtpSSRC
                               + " Size=" + e.Message.Data.Length);

            // Check the payload type in the RTP packet matches the Payload Type value from the SDP
            if (rtpPayloadType != videoPayloadType)
            {
                Tizen.Log.Info("JuvoPlayer", "Ignoring this RTP payload");
                return; // ignore this data
            }

            // If rtp_marker is '1' then this is the final transmission for this packet.
            // If rtp_marker is '0' we need to accumulate data with the same timestamp

            // ToDo - Check Timestamp
            // ToDo - Could avoid a copy if there is only one RTP frame for the data (temp list is zero)

            // Add the RTP packet to the tempoary_rtp list

            byte[] rtp_payload = new byte[e.Message.Data.Length - rtpPayloadStart]; // payload with RTP header removed
            Array.Copy(e.Message.Data, rtpPayloadStart, rtp_payload, 0, rtp_payload.Length); // copy payload
            temporary_rtp_payloads.Add(rtp_payload);

            if (rtpMarker == 1)
            {
                // End Marker is set. Process the RTP frame
                ProcessRTPFrame(temporary_rtp_payloads);
                temporary_rtp_payloads.Clear();
            }
        }

        // Process an RTP Frame. A RTP Frame can consist of several RTP Packets
        public void ProcessRTPFrame(List<byte[]> rtpPayloads)
        {
            Tizen.Log.Info("JuvoPlayer", "RTP Data comprised of " + rtpPayloads.Count + " rtp packets");

            List<byte[]> nalUnits = new List<byte[]>(); // Stores the NAL units for a Video Frame. May be more than one NAL unit in a video frame.

            for (int payloadIndex = 0; payloadIndex < rtpPayloads.Count; payloadIndex++)
            {
                // Examine the first rtp_payload and the first byte (the NAL header)
                int nal_header_f_bit = (rtpPayloads[payloadIndex][0] >> 7) & 0x01;
                int nal_header_nri = (rtpPayloads[payloadIndex][0] >> 5) & 0x03;
                int nalHeaderType = (rtpPayloads[payloadIndex][0] >> 0) & 0x1F;

                // If the Nal Header Type is in the range 1..23 this is a normal NAL (not fragmented)
                // So write the NAL to the file
                if (nalHeaderType >= 1 && nalHeaderType <= 23)
                {
                    Tizen.Log.Info("JuvoPlayer", "Normal NAL");

                    nalUnits.Add(rtpPayloads[payloadIndex]);
                }
                // There are 4 types of Aggregation Packet (split over RTP payloads)
                else if (nalHeaderType == 24)
                {
                    Tizen.Log.Info("JuvoPlayer", "Agg STAP-A");

                    // RTP packet contains multiple NALs, each with a 16 bit header
                    //   Read 16 byte size
                    //   Read NAL
                    try
                    {
                        int ptr = 1; // start after the nal_header_type which was '24'
                        // if we have at least 2 more bytes (the 16 bit size) then consume more data
                        while (ptr + 2 < (rtpPayloads[payloadIndex].Length - 1))
                        {
                            int size = (rtpPayloads[payloadIndex][ptr] << 8) + (rtpPayloads[payloadIndex][ptr + 1] << 0);
                            ptr = ptr + 2;
                            byte[] nal = new byte[size];
                            System.Array.Copy(rtpPayloads[payloadIndex], ptr, nal, 0, size); // copy the NAL
                            nalUnits.Add(nal); // Add to list of NALs for this RTP frame. Start Codes like 00 00 00 01 get added later
                            ptr = ptr + size;
                        }
                    }
                    catch
                    {
                        // do nothing
                    }
                }
                else if (nalHeaderType == 25)
                {
                    Tizen.Log.Info("JuvoPlayer", "Agg STAP-B not supported");
                }
                else if (nalHeaderType == 26)
                {
                    Tizen.Log.Info("JuvoPlayer", "Agg MTAP16 not supported");
                }
                else if (nalHeaderType == 27)
                {
                    Tizen.Log.Info("JuvoPlayer", "Agg MTAP24 not supported");
                }
                else if (nalHeaderType == 28)
                {
                    Tizen.Log.Info("JuvoPlayer", "Frag FU-A");
                    // Parse Fragmentation Unit Header
                    int fu_header_s = (rtpPayloads[payloadIndex][1] >> 7) & 0x01;  // start marker
                    int fu_header_e = (rtpPayloads[payloadIndex][1] >> 6) & 0x01;  // end marker
                    int fu_header_r = (rtpPayloads[payloadIndex][1] >> 5) & 0x01;  // reserved. should be 0
                    int fu_header_type = (rtpPayloads[payloadIndex][1] >> 0) & 0x1F; // Original NAL unit header

                    Tizen.Log.Info("JuvoPlayer", "Frag FU-A s=" + fu_header_s + "e=" + fu_header_e);

                    // Check Start and End flags
                    if (fu_header_s == 1 && fu_header_e == 0)
                    {
                        // Start of Fragment.
                        // Initiise the fragmented_nal byte array
                        // Build the NAL header with the original F and NRI flags but use the the Type field from the fu_header_type
                        byte reconstructed_nal_type = (byte)((nal_header_f_bit << 7) + (nal_header_nri << 5) + fu_header_type);

                        // Empty the stream
                        fragmentedNAL.SetLength(0);

                        // Add reconstructed_nal_type byte to the memory stream
                        fragmentedNAL.WriteByte(reconstructed_nal_type);

                        // copy the rest of the RTP payload to the memory stream
                        fragmentedNAL.Write(rtpPayloads[payloadIndex], 2, rtpPayloads[payloadIndex].Length - 2);
                    }

                    if (fu_header_s == 0 && fu_header_e == 0)
                    {
                        // Middle part of Fragment
                        // Append this payload to the fragmented_nal
                        // Data starts after the NAL Unit Type byte and the FU Header byte
                        fragmentedNAL.Write(rtpPayloads[payloadIndex], 2, rtpPayloads[payloadIndex].Length - 2);
                    }

                    if (fu_header_s == 0 && fu_header_e == 1)
                    {
                        // End part of Fragment
                        // Append this payload to the fragmented_nal
                        // Data starts after the NAL Unit Type byte and the FU Header byte
                        fragmentedNAL.Write(rtpPayloads[payloadIndex], 2, rtpPayloads[payloadIndex].Length - 2);

                        // Add the NAL to the array of NAL units
                        nalUnits.Add(fragmentedNAL.ToArray());
                    }
                }

                else if (nalHeaderType == 29)
                {
                    Tizen.Log.Info("JuvoPlayer", "Frag FU-B not supported");
                }
                else
                {
                    Tizen.Log.Info("JuvoPlayer", "Unknown NAL header " + nalHeaderType + " not supported");
                }

            }

            // Output all the NALs that form one RTP Frame (one frame of video)
            OutputNAL(nalUnits);
        }


        // RTSP Messages are OPTIONS, DESCRIBE, SETUP, PLAY etc
        private void RtspMessageReceived(object sender, Rtsp.RtspChunkEventArgs e)
        {
            RtspResponse message = e.Message as RtspResponse;
            if (message.OriginalRequest == null)
                return;

            Tizen.Log.Info("JuvoPlayer", "Received " + message.OriginalRequest.ToString());

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
            Tizen.Log.Info("JuvoPlayer", "Got reply from Play  " + message.Command);
        }

        private void ProcessSetupRequest(RtspResponse message)
        {
            // If we get a reply to SETUP (which was our third command), then process then send PLAY

            // Got Reply to SETUP
            Tizen.Log.Info("JuvoPlayer", "Got reply from Setup. Session is " + message.Session);

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
            Tizen.Log.Info("JuvoPlayer", System.Text.Encoding.UTF8.GetString(message.Data));

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

                    // seach the atributes for control, fmtp and rtpmap
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
                    videoDataChannel = udpPair.dataPort;     // Used in DataReceived event handler
                    videoRTCPChannel = udpPair.controlPort;  // Used in DataReceived event handler
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
                buffer.WriteData(new byte[] { 0x00, 0x00, 0x00, 0x01 });  // Write Start Code
                buffer.WriteData(nalUnit);           // Write NAL
            }
        }
    }
}