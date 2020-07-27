JuvoPlayer
=======

## Introduction

 JuvoPlayer provides framework for streaming video playback applications that is designed to be platform specific and component agnostic. It leverages independence from device firmware updates (OTNs) and offers greater implementation flexibility to the application. The source code is open and can be included in the software products under the MIT license conditions.

 Please read the articles listed below before starting development:
- [Dependencies](./docs/dependencies.md)
- [Setup](./docs/setup-instructions.md)
- [Launching](./docs/launching.md)
- [Debugging](./docs/debugging.md)
- [Release notes](./docs/release-notes.md)

## Concept

![JuvoPlayer Concept Diagram](./docs/img/jpconcept.svg)

Diagram illustrates high level concept. Dashed lined blocks represent external components/systems. Dotted blocks represent generalized elements.
Blocks in areas surrounded by dotted lines are the actual JuvoPlayer structures. Dark blue blocks are 3rd party libraries used as dependencies.

### User Interfaces

Four gray blocks in the left upper corner are in charge of the look & feel of the application. The major differences between UIs are different frameworks they're based on:

- JuvoPlayerXamarin - XamarinForms implementation. Portable to platforms supported by XamarinForms, i.e. Android, iOS, Windows, Tizen .Net
- JuvoReactNative - React Native Tizen .Net implementation. A fork of MS React Native .Net open source project. React Native Tizen .Net offers basic UI controls and bindings.
- JuvoOpenGLNative - NUI based application with OpenGL UI implemented using custom made C++ UI library. Offers excellent animation performance and UI design flexibility.
- SimplePlayer - Simplistic UI based on XamarinForms app template.

### Core: Common Set of Libraries

User Interfaces are implemented as separate Smart TV applications containing common libraries (Core). Responsibilities of the blocks are self-explanatory. As an example, the 'DashDataProvider' class prepares all the data from requested MPEG DASH .mpd (manifest file) for playback.

The 'Player module' represents the JuvoPlayer/Player folder contents. It feeds the Tizen TV platform player with extracted elementary stream packets. This operation is executed using the ES playback API (contents of the dashed 'Tizen .Net' block).

The 'FFmpeg' represents a set of included FFmpeg C libraries. It does the video content demuxing and/or downloading, depending on the applied video streaming protocol.

The MPEG DASH is just one of the supported streaming protocols. Other ones are HLS, RTP/RTSP and HTTP progressive, managed by 'FFmpeg' and 'SharpRTSP' blocks. Whenever a new streaming protocol implementation is needed, it can be added in a form of a library and distributed as a part of the JuvoPlayer core. This feature reveals flexibility of the architecture.

### Secured Content

Premium VOD services use DRM secured video content. Secured content playback is executed using Common Encryption Interface (CENC). The 'Player module' delegates this job to the JuvoPlayer's DRM module. It provides decryption and key management services. Decrypted content is stored inside Trust Zone with no application access to decrypted data.
Following DRMs are supported:
- PlayReady
- Widevine
