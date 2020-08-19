[Home](../README.md)

## Debugging

### UDP Logger
JuvoLogger.Udp allows JuvoPlayer log capture via UDP. UDP log capture can be used on devices and emulators which do not provide access to console log functionality. When enabled, JuvoPlayer console log is not available. Usage of UDP logger does require application repacking after modification of file:

```javascript
  juvo-player/Configuration/res/logger.config
```

Enabling UDP Logger:

1. Enable UDP logging by removing comments from UdpLogger section and specifying listen Port value. 
   Provided example uses Port 2222.

```javascript
   [LogLevel]
   JuvoPlayer=Info

   ; Uncomment UdpLogger section and provide listening Port value to enable UDP logging.
   ;
   [UdpLogger]
   Port=2222  
```
2. In order to connect to a host device, use any UDP client software, e.g. ncat (https://nmap.org/ncat/). 
3. Connect to UDP Logger from a client PC using following console command:
```javascript
  ncat -u <IP of device> <Port>
  ```
>  
> Upon connection, send any data to JuvoPlayer to initialize logging service. If ncat is used, simply press enter key. If everything is working fine, following information should be received by client: 
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

### React Native Tizen .Net Live coding  
> It is possible to launch and work on the JuvoReactNative GUI using facebook 'hot module reloading' engine (https://facebook.github.io/react-native/blog/2016/03/24/introducing-hot-reloading.html). To configure it follow the guide below:

#### TV emulator
  1. Launch Tizen TV emulator. 
  2. Set the port redirection in 'Emulator control panel->Network' menu: 
     * Source (Local host) port = 9998 (can be any available port, but the same as in the "config"->"tvip" port)
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
