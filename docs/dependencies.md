[Home](../README.md)

## Dependencies
1. [FFmpeg 3.3.6 'Hilbert'][ffmpeglink] - library (binaries) acting:

   [ffmpeglink]:https://www.ffmpeg.org/download.html#release_3.3
   
 * HLS protocol scenario's downloader and demuxer.
 * RTP/RTSP protocol based scenario's demuxer. 
 * MPEG DASH protocol based scenario's demuxer.  
2. [https://github.com/ngraziano/SharpRTSP][rtsplink] (RTP/RTSP module, 23rd December 2018) - RTP/RTSP protocol scenarios downloader

  [rtsplink]: https://github.com/ngraziano/SharpRTSP
  
3. Samsung Tizen TV firmware for 2019, 2020 TVs, including the Tizen 5.x TV emulator distributed with the Tizen Studio SDK 3.7 and newer.
4. Video content URLs embedded in videoclips.json files. See in the project tree:
* _Resources\videoclips.json_

5. React Native Tizen (https://github.com/Samsung/react-native-tizen-dotnet) based on react-native 0.42 and react-native-windows 0.42.
   * Nodejs - https://nodejs.org/en/download/
   * Yarn - https://yarnpkg.com/en/
   
   Important
   > Node.js versions higher than 12.10 were affected by regular expression syntax issue (https://github.com/facebook/react-native/issues/26598) which impacts the React Native Tizen dependencies update (npm or yarn command). If it happens, try downgrading Nodejs to version 12.10.
   
   [smarthubprevlink]: https://developer.samsung.com/tv/develop/guides/smart-hub-preview
