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
  
3. Samsung Tizen TV firmware for 2019, 2020 TVs, including the Tizen 5.x TV emulator distributed with the Tizen Studio SDK 3.7 and newer.
4. Video content URLs embedded in videoclips.json files. See in the project tree:
* _Resources\videoclips.json_

5. React Native Tizen (https://github.com/Samsung/react-native-tizen-dotnet) based on react-native 0.42 and react-native-windows 0.42.
   * Nodejs - https://nodejs.org/en/download/
   * Yarn - https://yarnpkg.com/en/
   
   Important
   > The Nodejs versions higher than 12.10 were affected by regular expression issue (https://github.com/facebook/react-native/issues/26598) which impacts the React Native Tizen dependencies update (npm or yarn command). If it happens in Your case try downgrade Nodejs to version 12.10.
   
   [smarthubprevlink]: https://developer.samsung.com/tv/develop/guides/smart-hub-preview

## Setup instructions
1. Download .zip or clone the repository to Your local drive. 
2. Open the JuvoPlayer solution with Microsoft Visual Studio. See the articles regarding Tizen .Net TV environment setup here: [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]

[tizendotnettvlink]: https://developer.samsung.com/tv/tizen-net-tv 

3. Setup the nuget packages repository for the solution (restore NuGet packages)
4. Set one of the GUI projects as startup. Choose one of the following:
* XamarinPlayer
* JuvoPlayer.OpenGL
* JuvoReactNative
* SimplePlayer

##### Important
> Due to it's 'hybrid' nature, The JuvoReactNative projects requires additional step. Before the first build or any JS part modifications it needs creation of the bundles to be included in the Tizen .NET shared\res folder. See the 'React Native bundle preparation' for details. 
    
### React Native bundle preparation
* Make sure Nodejs and Yarn have been successfully installed in Your system.
* Go to the JuvoReactNative root directory in the command line window.
* Type the `yarn` command without any parameters. Wait until all the dependencies have been installed.
* Release mode
  - Type: `yarn bundle`
  - Go and delete the JuvoReactNative\Tizen\shared\res\assets\assets folder which is redundant (React Native Tizen bundle script issue: https://github.com/Samsung/react-native-tizen-dotnet/issues/30#issue-533804121)
  - Build the JuvoReactNative with MS Visual Studio.
* Debug mode   
  - Follow the 'Release mode' instructions but replace the first step with:  `yarn bundle --dev` command.

## Application launch 

1. Connect with a TV set (or emulator) using the Device Manager tool (member of SDK) installed together with the Tizen Tools (SDK) see more in [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]
2. Start the select in Visual Studio solution tree GUI project by pressing F5 (debug) or ctrl+F5 (release). The app requires Partner Level privilege generated with the Certificate Manager tool (member of SDK). Before the first launch please, make sure that you have created one and have sent it by 'Permit to install' command using the Device Manager tool.

##### Important
> Every physical Tizen TV device needs a set of electronic signatures (keys) for successful launching of the Tizen .Net apps. To get the keys a developer has to contact Samsung Content Manager (CM) person in his country of residence. The https://seller.samsungapps.com/tv/ site can be used to facilitate this step for all interested business partners. As soon as the CM agrees to the contract details and confirms the company's 'Partner' status, the signatures will be shared by Samsung R&D over the seller.samsung.com site. This step is mandatory since the JuvoPlayer uses sensitive API's (native binaries and DRM access). There is no need to make such a request in case of launching the JuvoPlayer on the Tizen TV emulator. 

#### Tizen TV Emulator 2019, 2020 (Tizen 5.x) 
 
  > 1. All JuvoPlayer based applications contain complementary native binaries for ARM and x86. They are located in subfolders with the appropriate names (lib\x86, lib\arm).
  > 2. The consequence of selecting the clean video in resolutions higher than 640×480 (VGA) is significant playback smoothness degradation. The reason of this is limited performance of the TV emulator. This drawback does not appear on the physical TV set units.
  > 3. The Tizen TV emulator does not playback any DRM'ed contents (PlayReady, Widevine).

## Debugging

### Logger UDP
The JuvoLogger.Udp allows JuvoPlayer log capture, via UDP, from devices which do not provide access to console logging. Usage of UDP logger does require application repacking after modification of the logger.config file. This can be done using the MS Visual Studio's 'Rebuild' option. 
Editing in source code tree, logger.config is located in:
  * <application_root>/XamarinPlayer/XamarinPlayer.Tizen.TV/res
  * <application_root>/JuvoPlayer.OpenGL/res
  * <application_root>/JuvoReactNative/Tizen/res

To make the logger work follow the steps:
1. Enable UDP logging by adding the listening port value to config file. Port# defines listening port (example port: 2222) to which client can "connect to"
```javascript
  JuvoPlayer=Info
  UdpPort=<Port#>  
  ```
2. Start application or build and launch
3. In order to connect to device, use any UDP client software, like netcat (https://nmap.org/ncat/). 

4. Connect to JuvoPlayer typing with Your PC CLI and confirm by enter key:
```javascript
  ncat -u <IP of device> <Port>
  ```
>  
> Upon connection, send any data to JuvoPlayer to "connect to" logging service. If everything is working fine, following information should be received by the client: 
> 
 ```javascript
    ****************************
    *   JuvoPlayer UDP logger  *
    *                          * 
    * 1 UDP packet to:         *
    *   - stop output          *
    *     logs will be dropped *
    *   - start output         *
    *   - hijack connection    *
    ****************************
    *         Started          *
    ****************************
  ```
> Hint
> 
>  It happens that on Windows 10 (ncat), more than just one 'enter' click is needed to see the log messages on the screen. Please, try to confirm connection several times if it fails at first.

### React Native Tizen .Net Live coding  
> It is possible to launch and work on the JuvoReactNative GUI using facebook 'hot module reloading' engine (https://facebook.github.io/react-native/blog/2016/03/24/introducing-hot-reloading.html). To configure it follow the below guide:

#### TV emulator
  1. Launch Tizen TV emulator. 
  2. Set the port redirection in 'Emulator control panel->Network' menu: 
     * Source (Local host) port = 9998 (can be any free value but the same as in the "config"->"tvip" port)
     * to destination  (10.0.2.15) port = 8081 (npm server port)

  ##### Important
  > Step 2. needs to be repeated every time the TV emulator reboots.

  3. Edit package.json in the application root folder writing: 
  ```javascript
  "config": {
    "tvip": "127.0.0.1:9998",
    "mode": "Debug"
  },
  ```
  4. Type `yarn bundle --dev` command from the JuvoReactNative folder level.
  5. Delete the redundant assets folder (see the 'Setup instructions' section).
  6. Switch to 'Debug' build mode of Tizen .NET project (here it is named JuvoReactNative).
  7. Start build (using MS Visual Studio)
  8. Type command `npm run server`
  9. Launch the application. You may use PC ctrl+F5 keys in MS Visual Studio or TV remote control keys. The latter case does make sense only in the TV set or TV emulator menu if the former (PC) launching have happened at least once.
  10. Press 'red' button on the remote control to open the configuration menu. Select (one by one) the 'Enable hot module reloading', 'Enable live reload', 'Set host ip' (enter the IP address of Your host PC) and 'Reload JavaScript' options. Each option selection closes the menu, so You need to reopen it with the 'red' button on the remote controller per option.  

  > There is one more item: 'Start JS Remote debugging' with Chrome on the host PC. Selecting it from the menu triggers the application's button press event, so working with it may be confusing. 

From now on You can modify JavaScript part of the application code and see the update result right after saving it on the PC.

#### TV set unit
1. Switch on the TV set
2. Edit package.json in the application root folder writing: 
  ```javascript
  "config": {
    "tvip": "192.168.137.4",  
    "mode": "Debug"
  },
//The "tvip": "192.168.137.4" needs to be replaced with the actual IP address of the developer's TV set.
  ```
3. Includes all the steps from 4. to 10. described previously in the 'Launch TV emulator' section.

---

## Release notes
**JuvoPlayer 1.5.4 (beta)**
### Features:
* All features of the JuvoPlayer 1.5.3 
* JuvoReactNative GUI
  * Fix for defect in switching to another application and back (multitasking) with playback over RTSP.
* JuvoPlayer backend
  * Workaround for the 'playback start operation results in the app crash run on 2020 TV emulator (Tizen 5.5)' issue. This patch gonna be included until the Tizen TV emulator is ready to use the fixed elementary stream player API. With this patch all the JuvoPlayer GUI apps can play video with the Tizen 5.5 TV (2020) emulator. 
  * The logger.UDP module for collecting messages from the retail TV devices. The module works in readonly mode and provides messages signaled inside the JuvoPlayer code. See more in the 'Debugging' section.
### Known issues:
* Short video pause after seeking HLS content. Side effect of FFmepg's seek implementation.


**JuvoPlayer 1.5.3 (beta)**
### Features:
* All features of the JuvoPlayer 1.5.1
* Support for Tizen.Sdk version up to 1.0.9
* Xamarin UI and OpenGL: Common resources (tiles, videoclips.json) excluded to a separate project.
* JuvoPlayerOpenGL 
  * Animated focused video clip tiles added
* JuvoReactNative GUI
  * Playback settings view presents default values.
  * Resume playback after switching to another application and back (support for multitasking).
  * Incorporating NavigationExperimental component. Changed the way of handling the remote control events in 'views'.
* JuvoPlayerXamarin GUI
  * XamarinForms library version up to "4.4.0.991640" 
* JuvoPlayer backend 
  * DASH representation change during segment download
  * Stability improvements in switching to another application and back (multitasking).
  * Fix for the issue: 'FFW and REW operations on the sample 4K HEVC video does not end'.
### Known issues:
* Switching to another application and back (multitasking) does not work with playback over RTSP.
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
* The playback start operation results in the app crash run on 2020 TV emulator (Tizen 5.5). Issue discovered in the TV platform API. It does not appear on actual 2020 TV set hardware. Fix is expected with the next Tizen SDK release.

**JuvoPlayer 1.5.1 (beta)**
### Features:
* All features of the JuvoPlayer 1.5.0
* JuvoReactNative GUI 
  * Smart Hub preview deep links launching
  * Tizen 5.0 emulator runtime support
* JuvoPlayerXamarin GUI
  * FFW and REW progress bar frame preview added
  * Animating focused video clip tiles added
  * Rounded corners of the tiles on the list of videos
* JuvoPlayer backend 
  * Buffering event notification issue fix
  * Missing seek completion signaling issue fix
  * Switching off the MPEG DASH adaptive streaming when run on the TV emulator. It makes playback stick to the lowest quality representation but improves comfort of testing on the emulator.
### Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
* JuvoReactNative GUI playback settings view does not support setting default values (limitation of the React Native Tizen's Picker component).
* JuvoReactNative GUI does not resume playback after switching from another app (no support for multitasking).
* The FFW and REW operations on the sample 4K HEVC video does not end.
* The FFW and REW operations on MPEG DASH sample videos result in app crash on the 2020 TV emulator (Tizen 5.5).

**JuvoPlayer 1.5.0 (beta)**
### Features:
* All features of the JuvoPlayer 1.4.9
* JuvoReactNative GUI - based on React Native Tizen project https://github.com/Samsung/react-native-tizen-dotnet
* JuvoPlayerXamarin JuvoPlayerOpenGL animation and picture loading performance improvements.
* Bixby (voice control) basic playback functions (JuvoPlayerXamarin, JuvoPlayerOpenGL, JuvoReactNative )
* JuvoPlayer backend stability and performance improvements 
### Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
* JuvoReactNative seek in HLS, HTTP streams does not hide the activity indicator (missing seek completion signaling).
* JuvoReactNative GUI does not support deep linked shortcuts for SmartHub preview feature.
* JuvoReactNative GUI playback settings view does not support setting default values (limitation of the React Native Tizen's Picker component).
* JuvoReactNative GUI does not resume playback after switching from another app (no multitasking).

**JuvoPlayer 1.4.9 (beta)**
### Features:
* All features of the JuvoPlayer 1.4.8
* JuvoPlayer backend stability improvements 
### Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.
  
**JuvoPlayer 1.4.8 (beta)**
### Features:
* All features of the JuvoPlayer 1.4.7
* JuvoPlayer backend stability improvements
* Multitasking issue solved (see known issues in v1.4.7)
* HLS and MP4 over HTTP seek in stream (FFW, REW) function implementation.
### Known issues:
* Right after the finishing seek in HLS streams there is a short video pause until the audio catch up. It is a result of FFmpeg 'seek' function specific.

**JuvoPlayer 1.4.7 (beta)**
### Features:
* All features of the JuvoPlayer 1.4.6
* Static splash screens for Open GL and Xamarin GUIs
* JuvoPlayer backend stability improvements
### Known issues:
* Multitasking - switching between running apps - video sometimes do not recover

**JuvoPlayer 1.4.6 (beta)**
### Features:
* All features of the JuvoPlayer 1.4.5
* Update - sync with the original GitHub project - of the RTSP module code (see dependencies)
* Memory management optimizations (stability improvements)
* Support for x86 processors architecture (TV emulator)
* SimplePlayer GUI project added for illustrating simple playback scenario
### Known issues:
* Multitasking - switching between running apps - video sometimes does not recover

**JuvoPlayer 1.4.5 (beta)**
### Features:
* All features of the JuvoPlayer 1.4.4
* The FFW and REW 'in progress' on screen notification
* Live Stream sample change to 'Big Buck Bunny' video clip
### Known issues:
* RTP/RTSP playback does not start (regression)
* Not enough memory for UHD Widevine DRM'ed video (Tears of steel)
* Multitasking - switching between running apps - video sometimes does not recover

**JuvoPlayer 1.4.4 (beta)** 
### Features:
* All features of the JuvoPlayer 1.4.3
* The Smart Hub Preview related modifications (deep links backend change)
* Multitasking requirement implementation (https://developer.samsung.com/tv/develop/guides/fundamentals/multitasking)
* Widevine DRM'ed content playback (unstable)
### Known issues:
* RTP/RTSP playback does not start (regression)
* Not enough memory for UHD Widevine DRM'ed video (Tears of steel)
* Multitasking - switching between running apps - video sometimes does not recover

**JuvoPlayer 1.4.3 (beta)** includes:
1. Xamarin Forms and Tizen OpenGL Native based user interface capable to:
* Select video clip from a list
* Start of selected video playback
* Pause of video playback
* Stop of video playback
* Fast Forward & Rewind (MPEG-DASH only) 
2. MPEG DASH streaming protocol - fragmented and chunked mp4 (byte range) containers support.
3. MPEG DASH + PlayReady DRM decryption and playback of FHD and UHD content.
4. MP4 over HTTP protocol (download and demuxing by FFMPEG lib)
5. HLS (downloading and demuxing by FFMPEG)
6. RTP/RTSP (download by third party RTP library demuxing with FFMPEG)
7. SRT subtitles rendered in the clean HD, FHD, UHD VOD content streamed over MPEG-DASH protocol.
8. Web VTT subtitles rendered in the clean HD, FHD, UHD VOD content streamed over MPEG-DASH protocol.
9. Picture quality manual selection on VOD content streamed over the MPEG-DASH protocol.
10. Live MPEG DASH streams playback
11. Adaptive playback quality depending on the available bandwidth
12. The [Smart Hub Preview][smarthubprevlink] including deep link launch