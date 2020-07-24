JuvoPlayer
=======

## Introduction

 JuvoPlayer provides framework for streaming video playback applications that is designed to be platform specific and component agnostic. It leverages independence on device FW updates (OTNs) and offers greater implementation flexibility to the app. The source code is open and can be included in the software products under the MIT license conditions. 

 Please read the articles listed below before starting development:
- [Dependencies](./docs/dependencies.md)
- [Setup](./docs/setup-instructions.md)
- [Launching](./docs/launching.md)
- [Debugging](./docs/debugging.md)
- [Release notes](./docs/release-notes.md)

## Concept

![JuvoPlayer Concept Diagram](./docs/img/jpconcept.svg)

Diagram illustrates high level concept. Dashed lined blocks represent external components/systems. Dotted blocks show generalized elements. 
The blocks placed inside the doted lines areas are the actual JuvoPlayer structures. The dark blue blocks are the 3rd party libraries used as dependencies. 

### GUIs (skins)

The four, gray blocks in the left upper corner are in charge of the look & feel. Those blocks represent UI implementations differing by underlying framework usage:

- JuvoPlayerXamarin - XamarinForms implementation. Portable to platforms supported by XamarinForms, i.e. Android, iOS, Windows, Tizen .Net
- JuvoReactNative - React Native Tizen .Net implementation. A fork of MS React Native .Net open source project. React Native Tizen .Net offers basic UI controls and bindings.
- JuvoOpenGLNative - NUI based application with OpenGL UI implementation via custom made C++ UI library. Offers excellent animation performance and UI design flexibility.
- SimplePlayer - Simplistic UI based on XamarinForms app template.


### Core (common set of libraries)

GUIs are implemented as separate Smart TV applications containing common libraries (Core). The blocks are doing the job suggested by name. Example is the 'DashDataProvider' class. It prepares for playback all the data from requested MPEG DASH .mpd (manifest file).

 The 'Player module' represents the JuvoPlayer/Player folder contents. It feeds the Tizen TV platform player with extracted elementary stream packets. This operation is executed using the ES playback API (contents of the dashed 'Tizen .Net' block). 

The 'FFmpeg' represents binaries compiled in C. It does the video content demuxing or downloading, depending on the applied video streaming protocol.

The MPEG DASH is just one of the supported streaming protocols. Others are HLS and RTP/RTSP and HTTP progressive, managed by 'FFmpeg' and 'SharpRTSP' blocks. Whenever new streaming protocol implementation is needed, it can be added in form of library and distributed as part of the JuvoPlayer core. This feature reveals flexibility of the architecture.

### Secured Contents

Premium VOD services use DRM secured video. Secured content playback is executed via a common encryption interface (CENC). The 'Player module' delegates this job to the source code inside JuvoPlayer/Drms folder. On the diagram, it is called 'DRM module'. It provides decryption and key management services. Decrypted content is stored inside trust zone with no application access to decrypted data.
Following DRMs are supported
- PlayReady
- Widevine
