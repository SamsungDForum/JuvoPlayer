[Home](../README.md)

## Launching 

1. Connect to a TV set (or emulator) using a Device Manager tool (part of Tizen SDK - see more in [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]

[tizendotnettvlink]: https://developer.samsung.com/tv/tizen-net-tv 

2. Set chosen UI project as a StartUp Project in Visual Studio solution tree and run it by pressing F5 (Debug->Start Debugging) or Ctrl+F5 (Debug->Start Without Debugging). Application requires Partner Level Privilege Certificate generated with the Certificate Manager tool (part of Tizen SDK). Before the first launch, please make sure that you have created one and have sent it to a client device with a "Permit to install" command using the Device Manager tool.

##### Important
> Every physical Tizen TV device needs a set of electronic signatures (keys) for successful launching of a Tizen .Net app. To get the keys, a developer has to contact Samsung Content Manager (CM) person in his country of residence. The https://seller.samsungapps.com/tv/ site can be used to facilitate this step for all interested business partners. As soon as the CM agrees to the contract details and confirms the company's 'Partner' status, the signatures will be shared by Samsung R&D over the seller.samsung.com site. This step is mandatory since the JuvoPlayer uses sensitive API's (native binaries and DRM access). There is no need to make such a request in case of launching the JuvoPlayer on the Tizen TV emulator. 

#### Tizen TV Emulator 2019, 2020 (Tizen 5.x) 
 
  > 1. All JuvoPlayer based applications contain complementary native binaries for ARM and x86. They are located in subfolders with the appropriate names (lib\x86, lib\arm).
  > 2. A consequence of selecting a clean video content in resolutions higher than 640×480 (VGA) is significant playback smoothness degradation. The reason of this is limited performance of the TV emulator. This drawback does not affect physical TV set units.
  > 3. The Tizen TV emulator does not playback any DRM'ed contents (PlayReady, Widevine).