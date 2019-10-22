JuvoPlayer
=======
The reference for developers of streaming live TV and VOD Tizen .Net applications. The GUIs (alternative) are built on XamarinForms framework and Tizen Native with OpenGL. This sample illustrates how to utilize the elementary streams data source API (demuxed audio and video). The DRMed (MS PlayReady by CENC interface) and clean content can be played. MPEG DASH and RTP/RTSP content delivery protocols clients are integrated with the app (no TV platform dependency). The HLS protocol and demuxing of the streams are handled by the FFMPEG library incorporated as .so binary files.
## Dependencies
1. [FFmpeg 3.3.6 'Hilbert'][ffmpeglink] - library (binaries) acting:

   [ffmpeglink]:https://www.ffmpeg.org/download.html#release_3.3
   
 * HLS protocol scenario's downloader and demuxer
 * RTP/RTSP protocol based scenario's demuxer. 
 * MPEG DASH protocol based scenario's demuxer.  
2. [https://github.com/ngraziano/SharpRTSP][rtsplink] . (RTSP module, 23rd December 2018) - RTP/RTSP protocol scenarios downloader

  [rtsplink]: https://github.com/ngraziano/SharpRTSP
  
3. Samsung TV firmware for 2019 TVs 
4. Video content URLs embeded in videoclips.json files. See in the project tree:
* _XamarinPlayer.Tizen.TV\shared\res\videoclips.json_
* _JuvoPlayer.OpenGL\shared\res\videoclips.json_
* _JuvoReactNative\videoclips.json_
1. React Native Tizen (https://github.com/Samsung/react-native-tizen-dotnet) based on react-native 0.42 and react-native-windows 0.42.

[smarthubprevlink]: https://developer.samsung.com/tv/develop/guides/smart-hub-preview

## Setup instructions
1. Download .zip or clone the repository to your HDD. 
2. Open the JuvoPlayer solution with Microsoft Visual Studio. See the articles regarding Tizen .Net TV environment setup here: [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]

[tizendotnettvlink]: https://developer.samsung.com/tv/tizen-net-tv 

3. Setup the nuget packages repository for the solution (restore NuGet packages)
4. Set one of the GUI projects as startup. Choose one of the following:
* XamarinPlayer
* JuvoPlayer.OpenGL
* JuvoReactNative

## Application launch 
1. Connect with the TV set using the 'Device Manager' tool installed together with the Tizen Tools package see more in [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]
2. Start the select in Visual Studio solution tree GUI project by pressing F5 (debug) or ctrl+F5 (release)

## Features and release notes
**JuvoPlayer 1.5.0 (beta)**
1. Features:
* All features of the JuvoPlayer 1.4.9
* JuvoReactNative GUI - based on React Native Tizen project https://github.com/Samsung/react-native-tizen-dotnet
* JuvoPlayerXamarin JuvoPlayerOpenGL animation and picture loading performance improvements.
* Bixby (voice control) basic playback functions (JuvoPlayerXamarin, JuvoPlayerOpenGL, JuvoReactNative )
* JuvoPlayer backend stability and performance improvements 
2. Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
* JuvoReactNative seek in HLS, HTTP streams does not hide the activity indicator (missing seek completion signaling).
* JuvoReactNative GUI does not support deeplinked shortcuts for SmartHub preview feature.
* JuvoReactNative GUI playback settings view does not support setting default values (limitation of the React Native Tizen's Picker component).

**JuvoPlayer 1.4.9 (beta)**
1. Features:
* All features of the JuvoPlayer 1.4.8
* JuvoPlayer backend stability improvements 
2. Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
  
**JuvoPlayer 1.4.8 (beta)**
1. Features:
* All features of the JuvoPlayer 1.4.7
* JuvoPlayer backend stability improvements
* Multitasking issue solved (see known issues in v1.4.7)
* HLS and MP4 over HTTP seek in stream (FFW, REW) function implementation.
2. Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.

**JuvoPlayer 1.4.7 (beta)**
1. Features:
* All features of the JuvoPlayer 1.4.6
* Static splash screens for Open GL and Xamarin GUIs
* JuvoPlayer backend stability improvements
2. Known issues:
* Multitasking - switching between runing apps - video sometimes do not recover

**JuvoPlayer 1.4.6 (beta)**
1. Features:
* All features of the JuvoPlayer 1.4.5
* Update - sync with the original GitHub project - of the RTSP module code (see dependencies)
* Memory management optimizations (stability improvements)
* Support for x86 processors architecture (TV emulator)
* SimplePlayer GUI project added for illustrating simple playback scenario
2. Known issues:
* Multitasking - switching between runing apps - video sometimes do not recover

**JuvoPlayer 1.4.5 (beta)**
1. Features:
* All features of the JuvoPlayer 1.4.4
* The FFW and REW 'in progress' on screen notification
* Live Stream sample change to 'Big Buck Bunny' video clip
2. Known issues:
* RTP/RTSP playback does not start (regresion)
* Not enoght memory for UHD Widevine DRMed video (Tears of steel)
* Multitasking - switching between runing apps - video sometimes do not recover

**JuvoPlayer 1.4.4 (beta)** 
1. Features:
* All features of the JuvoPlayer 1.4.3
* The Smart Hub Preview related modifications (deep links backend change)
* Multitasking requirement implementation (https://developer.samsung.com/tv/develop/guides/fundamentals/multitasking)
* Widevine DRMed content playback (unstable)
2. Known issues:
* RTP/RTSP playback does not start (regresion)
* Not enoght memory for UHD Widevine DRMed video (Tears of steel)
* Multitasking - switching between runing apps - video sometimes do not recover

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