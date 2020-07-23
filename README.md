JuvoPlayer
=======

## Introduction

This project provides an application level video streaming player framework. It links the video content delivery networks (CDNs) with the Tizen .Net TV platform. The design introduces decoupling layer from the HW platform specifics. By that it gives more responsibility to the application side. The output advantage is less time needed for the application launch to the market or updates during its life cycle. It simply gets much less dependent on the TV device firmware releases plan.

JuvoPlayer can be used as reference for developers of live streaming and VOD Tizen .Net applications. The source code is open, so it can be included as a whole or just parts inside the 3rd party software products.

 Please, read the below articles before starting the development:
- [Dependencies](./docs/dependencies.md)
- [Setup](./docs/setup-instructions.md)
- [Launching](./docs/launching.md)
- [Debugging](./docs/debugging.md)
- [Release notes](./docs/release-notes.md)

## The concept explanation

![JuvoPlayer Concept Diagram](./docs/img/jpconcept.svg)

The diagram shows up high level concept of the JuvoPlayer. For simplicity only part of the libraries and it's contents are visible. The blocks surrounded with dashed lines represent the external systems. Here it means not only the services communicating over the Web but also the APIs provided by the Tizen TV set. 

The blocks placed inside the doted lines areas are the actual JuvoPlayer structures. The dark blue blocks are the 3rd party libraries used as dependencies. 

### The GUI (Skins)

The four, gray blocks in the left upper corner are in charge of the look & feel of the application. The major differences between the GUIs (Skins) are related to different frameworks applied:

- JuvoPlayerXamarin -  XamarinForms which is portable and available for Android, iOS, MS Windows and Tizen .Net
- JuvoReactNative - React Native Tizen .Net is a fork (extension) of the MS React Native .Net open source project. Includes basic Tizen platform UI controls bindings.
 - JuvoOpenGLNative - the NUI template based application. Contains UI library coded from scratch in C++. Thanks to the Open GL lib inclusion it provides excellent performance in animation fluency. 
- SimplePlayer - is just very basic UI (single button and label) constructed on the XamarinForms app template

### Common set of libraries (Core)

All the GUIs (Skins) are distributed as separate Smart TV applications. Each of it contains the common set of libraries (Core). The Core (a big, dashed line bordered block) contains the 3rd party libs (gray and dark blue).  The blocks are doing the job suggested by it's name. 

Example is the 'DASH MpdParser'. It can parse all the data from requested MPEG DASH .mpd (manifest). After that the 'EsPlayer' (gray .Net class block) feeds the Tizen TV platform player with extracted elementary stream packets. This operation is executed using the ES playback API (contents of the dashed 'Tizen .Net' block). 

The FFmpeg block represents binaries compiled in C. It does the video content demuxing or downloading, depending on the applied video streaming protocol.

The MPEG DASH is just one of the supported streaming protocols. The others are HLS and RTP/RTSP and HTTP progressive, managed by - dark blue - FFmpeg and SharpRTSP blocks. Whenever new streaming protocol implementation is needed, it can be added in form of library and distributed as part of the JuvoPlayer core. This feature reveals flexibility of the architecture.

### Secured Contents

Premium VOD services need mechanism to access and decode DRM secured video contents. The JuvoPlayer handles such scenarios using common encryption interface (CENC). The 'EsPlayer' class delegates this job to the' CencSession' class. With that it is capable to check content authority as well as gather the deciphering keys. As soon as the secured contents are downloaded, the next tasks, deciphering and on-screen presentation, are executed by the TV set in it's trusted zone.