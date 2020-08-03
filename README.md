JuvoPlayer
=======

## Introduction

 JuvoPlayer is a reference player application designed to be platform and component agnostic, leveraging dependence on device firmware updates (OTNs) and offering greater implementation flexibility. Source code is open and can be included in the software products under the MIT license conditions.

 Please read the articles listed below before starting development:
- [Dependencies](./docs/dependencies.md)
- [Setup](./docs/setup-instructions.md)
- [Launching](./docs/launching.md)
- [Debugging](./docs/debugging.md)
- [Release notes](./docs/release-notes.md)

## Concept

![JuvoPlayer Concept Diagram](./docs/img/jpconcept.svg)

High level conceptual diagram. Dashed lined blocks represent external components/systems. Dotted blocks represent generalised elements.
Dotted line surrounded blocks represent JuvoPlayer structures. Dark blue blocks are 3rd party libraries.

### User Interfaces

Four grey blocks in the left upper corner represent UIs. UIs differ by underlying framework.

- JuvoPlayerXamarin - XamarinForms implementation. Portable to platforms supported by XamarinForms, i.e. Android, iOS, Windows, Tizen .Net
- JuvoReactNative - React Native Tizen .Net implementation. A fork of MS React Native .Net open source project. React Native Tizen .Net offers basic UI controls and bindings.
- JuvoOpenGLNative - NUI based application with OpenGL UI implemented using custom made C++ UI library. Offers excellent animation performance and UI design flexibility.
- SimplePlayer - Simplistic UI based on XamarinForms app template.

### Core: Common Set of Libraries

User Interfaces are implemented as separate Smart TV applications containing common libraries (Core). Responsibilities of the blocks are self-explanatory. As an example, the 'DashDataProvider' class prepares all the data from requested MPEG DASH .mpd (manifest file) for playback.

'Player module' represents the JuvoPlayer/Player folder content. It feeds the Tizen TV platform player with extracted elementary stream packets.

'FFmpeg' - FFmpeg C libraries.

MPEG-DASH, HLS, RTP/RTSP and Progressive HTTP streaming protocols are supported. 
New streaming protocols can be added and distributed as a part of the JuvoPlayer core.


### Secured Content

Premium VOD services use DRM secured content. Secured content playback is executed using Common Encryption Interface (CENC). JuvoPlayer's DRM module provides decryption and key management services. Decrypted content is stored inside TrustZone with no application access to decrypted data.
Following DRMs are supported:
- PlayReady
- Widevine
