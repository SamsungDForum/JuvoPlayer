[Home](../README.md)

## Launching 

1. Connect with a TV set (or emulator) using the Device Manager tool (member of SDK) installed together with the Tizen Tools (SDK) see more in [https://developer.samsung.com/tv/tizen-net-tv][tizendotnettvlink]
2. Start the select in Visual Studio solution tree GUI project by pressing F5 (debug) or ctrl+F5 (release). The app requires Partner Level privilege generated with the Certificate Manager tool (member of SDK). Before the first launch please, make sure that you have created one and have sent it by 'Permit to install' command using the Device Manager tool.

##### Important
> Every physical Tizen TV device needs a set of electronic signatures (keys) for successful launching of the Tizen .Net apps. To get the keys a developer has to contact Samsung Content Manager (CM) person in his country of residence. The https://seller.samsungapps.com/tv/ site can be used to facilitate this step for all interested business partners. As soon as the CM agrees to the contract details and confirms the company's 'Partner' status, the signatures will be shared by Samsung R&D over the seller.samsung.com site. This step is mandatory since the JuvoPlayer uses sensitive API's (native binaries and DRM access). There is no need to make such a request in case of launching the JuvoPlayer on the Tizen TV emulator. 

#### Tizen TV Emulator 2019, 2020 (Tizen 5.x) 
 
  > 1. All JuvoPlayer based applications contain complementary native binaries for ARM and x86. They are located in subfolders with the appropriate names (lib\x86, lib\arm).
  > 2. The consequence of selecting the clean video in resolutions higher than 640×480 (VGA) is significant playback smoothness degradation. The reason of this is limited performance of the TV emulator. This drawback does not appear on the physical TV set units.
  > 3. The Tizen TV emulator does not playback any DRM'ed contents (PlayReady, Widevine).