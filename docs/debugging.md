[Home](../README.md)

## Debugging

### UDP Logger
JuvoLogger.Udp allows JuvoPlayer log capture via UDP. UDP log capture can be used on devices and emulators which do not provide access to console log functionality. When enabled, JuvoPlayer console log is not available. Usage of UDP logger does require application repacking after modification of configuration file:

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

#### Setup
  1. Launch Tizen TV emulator or start physical TV unit. 
  2. Type `yarn bundle --dev` command from the JuvoReactNative folder level.
  3. Delete the redundant assets folder (see the 'Setup instructions' section).
  4. Switch to 'Debug' build mode of JuvoReactNative project.
  5. Start build (using MS Visual Studio)
  6. Type command `npm run server`
  7. Launch the application. You may use PC ctrl+F5 keys in MS Visual Studio or TV remote control keys. The latter case does make sense only in the TV set or TV emulator menu if the former (PC) launching have happened at least once.
  8. Press 'red' button on the remote control to open the configuration menu. Select (one by one) the 'Enable hot module reloading', 'Enable live reload', 'Set host ip' (enter the IP address of Your host PC - in emulator case it's usually 10.0.2.2) and 'Reload JavaScript' options. Each option selection closes the menu, so You need to reopen it with the 'red' button on the remote controller per option.  

  > There is one more item: 'Start JS Remote debugging' with Chrome on the host PC. Selecting it from the menu triggers the application's button press event, so working with it may be confusing. 

From now on You can modify JavaScript part of the application code and see the update result right after saving it on the PC.

### Xamarin HotReload
HotReload allows updating xaml elements in real time. It is enabled by default in Debug mode.

#### Setup
1. Install HotReload extension corresponding to your IDE:

    JetBrains Rider:
    File -> Settings -> Plugins (pick "Marketplace" tab) -> search by "HotReload".

    Visual Studio for WINDOWS:
    Extensions -> Manage Extension -> search by "HotReload" -> Install extension and restart IDE
    Tools -> Customize... -> Check box next to "Xamarin.Forms Hot Reload"
    
2. Click "Enable Xamarin.Forms Hot Reload" button.
3. Install and run the XamarinPlayer app in Debug configuration.

#### TV emulator
1. Launch Tizen TV emulator.
2. Set the port redirection in 'Emulator control panel->Network' menu: 
     * Source (Local host) port = 8000
     * to destination  (10.0.2.15) port = 8000
    
     >Step 2. needs to be repeated every time the TV emulator reboots.

3. Follow all the steps in **Setup** section

#### Additional Setup / Troubleshooting

1. Cannot see extension buttons in Visual Studio

    Visual Studio Installer -> More -> Modify -> Workloads -> Install "Visual Studio extensions development"
    
2. HotReload plugin doesn't work for JetBrains Rider

    Check https://github.com/AndreiMisiukevich/HotReload/blob/master/Extension/Xamarin.Forms.HotReload.Extension.Rider/gradle.properties to see if your Rider version is currently supported.

3. Useful for troubleshooting

    Download https://github.com/AndreiMisiukevich/HotReload and manually launch the Xamarin.Forms.HotReload.Observer app.
