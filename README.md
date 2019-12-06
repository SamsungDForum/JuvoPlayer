JuvoPlayer
=======
The reference for developers of streaming live TV and VOD Tizen .Net applications. The GUIs (alternative) are built on XamarinForms framework and Tizen Native with OpenGL or React Native. This sample illustrates how to utilize the elementary streams data source API (demuxed audio and video). The DRMed (MS PlayReady by CENC interface) and clean content can be played. MPEG DASH and RTP/RTSP content delivery protocols clients are integrated with the app (no TV platform dependency). The HLS protocol and demuxing of the streams are handled by the FFMPEG library incorporated as .so binaries.
## Dependencies
1. [FFmpeg 3.3.6 'Hilbert'][ffmpeglink] - library (binaries) acting:

   [ffmpeglink]:https://www.ffmpeg.org/download.html#release_3.3
   
 * HLS protocol scenario's downloader and demuxer
 * RTP/RTSP protocol based scenario's demuxer. 
 * MPEG DASH protocol based scenario's demuxer.  
2. [https://github.com/ngraziano/SharpRTSP][rtsplink] . (RTSP module, 23rd December 2018) - RTP/RTSP protocol scenarios downloader

  [rtsplink]: https://github.com/ngraziano/SharpRTSP
  
3. Samsung TV firmware for 2019 TVs (integrated with the Tizen 5.0 TV emulator included in SDK 3.2)
4. Video content URLs embeded in videoclips.json files. See in the project tree:
* _XamarinPlayer.Tizen.TV\shared\res\videoclips.json_
* _JuvoPlayer.OpenGL\shared\res\videoclips.json_
* _JuvoReactNative\videoclips.json_
5. React Native Tizen (https://github.com/Samsung/react-native-tizen-dotnet) based on react-native 0.42 and react-native-windows 0.42.
   * Nodejs - https://nodejs.org/en/download/
   * Yarn - https://yarnpkg.com/en/
   
   Important
   > The Nodejs versions higher than 12.10 were affected by regular expression issue (https://github.com/facebook/react-native/issues/26598) which impacts the React Native Tizen dependencies update (npm or yarn command). If it happens in Your case try downgrade Nodejs to version 12.10.


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
* SimplePlayer

Important
> Due to it's 'hybrid' nature, The JuvoReactNative projects requires additinal step. Before the first build or any JS part modifications it needs creation of the bundles to be included in the Tizen .NET shared\res folder. See the 'React Native bundle preparation' for details. 
    
React Native bundle preparation
* Make sure Nodejs and Yarn have been succesfully instaled in Your system.
* Go to the JuvoReactNative root directory in the command line window.
* Type the `yarn` command without any parameters. Wait untill all the dependencies have been installed.
* Release mode
  1. Type `yarn bundle`
  2. Delete the JuvoReactNative\Tizen\shared\res\assets\assets fodler which is redundant (React Native Tizen bundle script issue: https://github.com/Samsung/react-native-tizen-dotnet/issues/30#issue-533804121)
  3. Build the JuvoReactNative with MS Visual Studio.
* Debug mode 
  1. Type `yarn bundle --dev`
  2. Follow the steps 2 and 3 of 'Release mode'.

## Application launch 
1. Connect with a TV set (or emulator) using the 'Device Manager' tool (SDK) installed together with the Tizen Tools (SDK) see more in [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]
2. Start the select in Visual Studio solution tree GUI project by pressing F5 (debug) or ctrl+F5 (release). The app requires Partner Level privilege generated widh the Certificate Manager tool (SDK). Before the first launch please, make sure that you have created one and have sent by 'Permit to install' command with the Device Manager tool (SDK).

Important
> Every phisical Tizen .NET TV device being used for JuvoPlayer application development needs to be equiped with a set of electronic signatures (security policy). To get the keys, a developer has to contact Samsung Content Manager (CM) person in his country of residence. The CM can request the signatures from Samsung R&D. This step is mandatory since the JuvoPlayer uses sensitive API's (native binaries and DRM access). There is no need to make such a request in case launching the JuvoPlayer on the TV emulator. In this case however, it is not possible to playback any DRM'ed contents (PlayReady, Widevine). Moreover, the consequence of the clean video playback in resolutions highier then 640×480 (VGA) is significant video frames drop. The reason of this latter phenomena is the TV emulator's performance limitation. This drawback do not appear on the phisical
 TV set units.

Live coding React Native Tizen application
> It is possible to launch and work on the JuvoReactNative GUI using facebook 'hot module reloading' engine (https://facebook.github.io/react-native/blog/2016/03/24/introducing-hot-reloading.html). To configure it follow the below guide:
* Emulator TV
  1. Launch TV emulator. 
  > JuvoReactNative application contains complementary binaries for ARM and x86. They are located in subfolders with the appropriate names. The React native Tizen applications based on the default template do not follow this rule. Launching it on emulator needs actions mentioned here: https://github.com/Samsung/react-native-tizen-dotnet/issues/18#issuecomment-521515750

  2. Set the port redirection in 'Emulator control panel->Network' menu: 
     * Source (Local host) port = 9998 (can be any free value but the same as in the "config"->"tvip" port)
     * to destination  (10.0.2.15) port = 8081 (npm server port)

  Important
  > Step 2. needs to be repeated every time the TV emulator reboots.
  3. Edit package.json in the application root folder writting: 
  ```javascript
  "config": {
    "tvip": "127.0.0.1:9998",
    "mode": "Debug"
  },
  ```
  4. Type `yarn bundle --dev` command (assuming current is the React Native Tizen application root folder).
  5. Delete the redundant assets folder (see the 'Setup instructions' section).
  6. Switch to 'Debug' build mode of tizen .NET project (here it is named JuvoReactNative).
  7. Start build 
  8. Type command `npm run server`
  9. Launch the application on PC (ctrl+F5 in VS) or a TV set (emulator) if this is a next try.
  10. Press 'red' button on the remote control to open the configuration menu. Select (one by one) the 'Enable hot module reloading', 'Enable live reload', 'Set host ip' (enter the IP address of Your host PC) and 'Reload JavaScript' options. Each option selection closes the menu, so You need to reopen it with the 'red' button on the remote controller per option.  

  > There is one more item: 'Start JS Remote debugging' with Chrome on the host PC. Selecting it from the menu triggers the application's button press event, so working with it may be confusing. 

From now on You can modify JavaScript part of the application code and see the update result right after saving it on the PC.

* TV set unit
1. Switch on the TV set
2. Edit package.json in the application root folder writting: 
  ```javascript
  "config": {
    "tvip": "192.168.137.4",  
    "mode": "Debug"
  },
  ```
  > The "tvip": "192.168.137.4" needs to be replaced with the actual IP address of the TV set.

3. Includes all the steps from 4. to 10. described previously in the 'Launch TV emulator' section.

## Features and release notes
**JuvoPlayer 1.5.1 (beta)**
1. Features:
* All features of the JuvoPlayer 1.5.0
* JuvoReactNative GUI 
  * Smart Hub preview deeplinks launching
  * Tizen 5.0 emulator runtime support
* JuvoPlayerXamarin GUI
  * FFW and REW progress bar frame preview added
  * Animating focused video clip tiles added
  * Rounded corners of the tiles on the list of videos
* JuvoPlayer backend 
  * Buffering event notification issue fix
  * Missing seek completion signaling issue fix
  * Switching off the MPEG DASH adaptive streaming when run on the TV emulator. It makes playback stick to the lowest quality representation but improves comfort of testing on the emulator.
2. Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
* JuvoReactNative GUI playback settings view does not support setting default values (limitation of the React Native Tizen's Picker component).
* JuvoReactNative GUI does not resume playback after switching from another app (no support for multitasking).
* The FFW and REW operations on the sample 4K HEVS video does not end.
* The FFW and REW operations on MPEG DASH sample videos result in app crash on the TV emulator.

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
* JuvoReactNative GUI does not resume playback after switching from another app (no multitasking).

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