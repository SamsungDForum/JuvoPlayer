JuvoPlayer
=======
The reference for developers of streaming live TV and VOD Tizen .Net applications. The GUIs (optional) are built on XamarinForms framework and Tizen Native with OpenGL. This sample illustrates how to utilize the elementary streams data source API (demuxed audio and video). The DRMed (MS PlayReady by CENC interface) and clean content can be played. MPEG DASH and RTP/RTSP content delivery protocols clients are integrated with the app (no TV platform dependency). The HLS protocol and demuxing of the streams are handled by the FFMPEG library incorporated as .so binary files.
## Dependencies
1. [FFmpeg 3.3.6 'Hilbert'][ffmpeglink] - library (binaries) acting:

   [ffmpeglink]:https://www.ffmpeg.org/download.html#release_3.3
   
 * HLS protocol scenarios downloader and demuxer
 * RTP/RTSP protocol based scenarios demuxer. 
 * MPEG DASH protocol based scenarios demuxer. * 
2. [https://github.com/ngraziano/SharpRTSP][rtsplink] . (RTSP module, 2nd March 2017 ) - RTP/RTSP protocol scenarios downloader

  [rtsplink]: https://github.com/ngraziano/SharpRTSP
  
3. Samsung TV 2019 models FW build - **This component comes with a Samsung 2019 TV set device**
4. Video content URLs embeded in videoclips.json files. See in the project tree:
* _XamarinPlayer\XamarinPlayer.Tizen.TV\shared\res\videoclips.json_
* _JuvoPlayer.OpenGL\shared\res\videoclips.json_

[smarthubprevlink]: https://developer.samsung.com/tv/develop/guides/smart-hub-preview

## Setup instructions
1. Download .zip or clone the repository to your HDD. 
2. Open the JuvoPlayer solution with Microsoft Visual Studio. See the articles regarding Tizen .Net TV environment setup here: [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]

[tizendotnettvlink]: https://developer.samsung.com/tv/tizen-net-tv 

3. Setup the nuget packages repository for the solution (restore NuGet packages)
4. Set one of the GUI projects as startup. Choose one of the following:
* XamarinPlayer
* JuvoPlayer.OpenGL

## Application launch 
1. Connect with the TV set using the 'Device Manager' tool installed together with the Tizen Tools package see more in [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]
2. Start the select GUI project by pressing F5 (debug) or ctrl+F5 (release)

## Features and release notes
**JuvoPlayer 1.4.3 (beta)** includes:
1. Xamarin Forms and Tizen OpenGL Native based user interface capable to:
* Select video clip from a list
* Start of selected video playback
* Pause of video playback
* Stop of video playback
* Fast Forward & Rewind (MPEG-DASH only) 
2. MPEG DASH streaming protocol - fragmented and chunked mp4 (byte range) containers support.
3. MPEG DASH + PlayReady DRM decryption and playbackof FHD and UHD content.
4. MP4 over HTTP protocol (download and demuxing by FFMPEG lib)
5. HLS (downloading and demuxing by FFMPEG)
6. RTP/RTSP (download by third party RTP library demuxing with FFMPEG) 
7. SRT subtitles rendered in the clean HD, FHD, UHD VOD content streamed over MPEG-DASH protocol.
8. Web VTT subtitles rendered in the clean HD, FHD, UHD VOD content streamed over MPEG-DASH protocol.
9. Picture quality manual selection on VOD content streamed over the MPEG-DASH protocol. 
10. Live MPEG DASH streams playback
11. Adaptive playback quality depending on the available bandwidth
12. The [Smart Hub Preview][smarthubprevlink] including deeplink launch 
