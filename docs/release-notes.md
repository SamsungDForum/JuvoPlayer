[Home](../README.md)

## Release notes

**JuvoPlayer 1.5.7 (beta)**

### Features:

* All features of the JuvoPlayer 1.5.6. 
* The MS Build scripting (.csproj) files are ready for multitarget builds, generating tizen50 and tizen60 packages in a single build (rebuild) step. 
  * Example:  
  ` <TargetFrameworks>tizen60;tizen50</TargetFrameworks>
  `  means that the two .tpk packages will be created as output artifacts. By default, MS Visual Studio will try to install and launch the first listed, in this example it will be tizen60.

   > IMPORTANT - The most recent Tizen Baseline SDK includes TV emulator compatible with Tizen 6.0 (TV 2021). Make sure you have installed Tizen Baseline SDK version 2.8.4 or later with all the necessary TV extensions (TV Extensions 6.0 and Samsung Certificate Extension version 2.0.54 or later). The SDK related version values can be found using the Tizen Studio Package Manager (see more in https://developer.samsung.com/tizen/Smart-TV/Installation.html). Check also the correct version (3.4.0.0 or later) of the Visual Studio Tools for Tizen managed as IDE extension (see more in https://marketplace.visualstudio.com/items?itemName=tizen.VSToolsforTizen). 
* The tizen-manifest.xml metafiles are no longer updated automatically. It used to happen when JuvoPlayer project version value was changing (version up). From now on, the common version value is placed inside the Directory.Build.Props, located in the solution root folder.

**JuvoPlayer 1.5.6 (beta)**

### Features:

* All features of the JuvoPlayer 1.5.5. 
* Integration tests are now automatically run using CircleCI.

* JuvoReactNative GUI
  * Fixes for security alerts reported to external dependencies. 
  * All UI apps are now using common remote videoclips.json metafile. 

* JuvoPlayerXamarin GUI
  * Added support for ‘Hot Reloading’ developer tool.
  
* JuvoPlayer backend
  * Fix for artifacts on the beginning of FHD Widevine encrypted MPEG DASH video.
  * FFmpeg and it's bindings have been moved to nuget.org as dependency packages. Current FFmpeg version: 3.3.6.
  * Running clock logic change.
  * Fix for ‘destructive’ representation change issue.
  * Re-factorization of JuvoPlayer’s DRM code.
  
* JuvoPlayer Documentation
  * Correction of UDP logger description.
  
  
### Known issues:
* Short video pause after seeking HLS content. Side effect of internal FFmpeg's seek implementation.


**JuvoPlayer 1.5.5 (beta)**

### Features:

* All features of the JuvoPlayer 1.5.4 
* JuvoReactNative GUI
  * Exit app after 'return' key press with the remote control.
* JuvoPlayerOpenGL 
  * Added UI notification for videoclips.json download error.
* JuvoPlayerXamarin GUI
  * UI notification when launching with invalid deeplink url
  * Adapted ContentListPage to MVVM pattern.
* JuvoPlayer backend
  * Fix for crash on TV emulator after 60 seconds of playback over RTSP.
  * Converted logger.config to INI format.
  * Fixes for Suspend Resume integration test failures.
* JuvoLogger.UDP
  * Fix issues when the logger would crash after cancelling an operation or while running Widevine encrypted content.
* JuvoPlayer Tests
  * Added RTSP stream to integration test.
* JuvoPlayer Documentation
  * Reorganized contents.
  * Added concept diagram.

### Known issues:
* Short video pause after seeking HLS content. Side effect of FFmepg's seek implementation.

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
