const getAppInfo = require('./getAppInfo');

var launchTarget = (function () {

    /*function getAppName() {

        if (APP_TYPE === '.wgt') {
            return 'app';
        }
        var dir = __dirname;
        var endIndex = dir.indexOf('/node_modules/tv-dev-cli-sdk/');
        var nameArray = dir.slice(0, endIndex).split('/');
        return nameArray[nameArray.length - 3];
    }*/

    var os = require('os');
    var fs = require('fs');
    var path = require('path');
    var innerProcess = require('child_process');

    var Q = require('q');

    // Use 'buildpakage.js' to refresh the App package
    var common = require('./common');

    var http = require('http');

    // default packing type
    var WGT = '.wgt';
    var TPK = '.tpk';
    var APP_TYPE = WGT;

    var SDB_NAME = (process.platform == 'win32') ? 'sdb.exe' : 'sdb';
    var SDB_FOLDER = (process.platform == 'win32') ? 'win' : (process.platform == 'linux') ? 'linux' : 'mac';
    var LIB_PATH = path.normalize('tools/sdb');
    var EXTENSION_PATH = __dirname;
    var OUTPPUT_PATH = 'output';

    var SPAWN_SDB_PATH = EXTENSION_PATH + path.sep + LIB_PATH + path.sep + SDB_FOLDER + path.sep + SDB_NAME;
    //var SDB_PATH = '\"' + SPAWN_SDB_PATH + '\"';
    var SDB_PATH = path.normalize(`\"${SPAWN_SDB_PATH}\"`);

    var workspacePath = '';
    var outputFullPath = '';
    var targetVersion = '2.4';
    var SPACE = ' ';
    var DEFAULT_IP = '255.255.255.255';
    var TARGET_IP = '255.255.255.255';


    // SDB command definition
    var SDB_COMMAND_CONNECT = 'connect';
    var SDB_COMMAND_DISCONNECT = 'disconnect';
    var SDB_COMMAND_INSTALL = 'install';

    var SDB_COMMAND_CAT = 'capability';

    var SDB_COMMAND_UNINSTALL = 'uninstall';

    //var SDB_COMMAND_GETWIDGETID = 'shell /usr/bin/wrt-launcher -l';
    //var SDB_COMMAND_LAUNCH = 'shell /usr/bin/wrt-launcher --start';

    //var SDB_COMMAND_DLOG = "shell /usr/bin/dlogutil -v time ConsoleMessage";
    //var SDB_COMMAND_DLOG = 'dlog -v time ConsoleMessage';

    //WAS command definition
    //var WAS_COMMAND_UNINSTALL = 'shell wascmd -u ';

    //var WAS_COMMAD_INSTALL = 'shell wascmd -i ';
    //var WAS_COMMAD_LAUNCH = 'shell wascmd -r';

    //sdb shell secure command
    var SDB_COMMAND_DLOG = 'shell 0 showlog time';
    var WAS_COMMAND_UNINSTALL = 'shell 0 vd_appuninstall ';
    var WAS_COMMAD_INSTALL = 'shell 0 vd_appinstall ';
    var WAS_COMMAD_LAUNCH = 'shell 0 was_execute ';

    var SDB_COMMAND_LAUNCH = 'shell 0 execute';
    var SDB_COMMAND_DEBUG = 'shell 0 debug';
    var TIME_OUT = '5000';


    var SDB_COMMAND_ROOT = 'root on';
    var SDB_COMMAND_START = 'start-server';
    var SDB_COMMAND_KILL = 'kill-server';

    var SDB_COMMAND_PUSH = 'push';
    var SDB_OPT_SERIAL = '-s ' + DEFAULT_IP;

    // set init global output log level and module
    var LOG_LEVEL = 'DEBUG';
    var moduleName = 'Run on TV: ';

    //Prepare wgt and connect devices before install and run app
    var prepareInstall = function (dirpath, targetAddress) {
        var deferred = Q.defer();
        console.log(moduleName + '================Prepare install');
        workspacePath = dirpath;
        // Check if the targe device IP has been setted
        var targetAddressStart = targetAddress.indexOf(DEFAULT_IP);
        if (targetAddressStart === 0) {
            var targetNotConfig = 'The target device address is not configured, please refer File->Preference->User Settings';
            console.log(moduleName + targetNotConfig);
            return;
        }

        // App folder path validation
        if (workspacePath) {
            var startHtml = 'index.html';
            var configFilePath = workspacePath + path.sep + 'config.xml';

            // Check if start html set
            var configuredHtml = common.getConfStartHtml(configFilePath);
            if (configuredHtml !== '') {
                startHtml = configuredHtml;
            }

            console.log(moduleName + 'workspacePath=' + workspacePath);
            //sdb command
            var disconnectCommand = SDB_PATH + SPACE + SDB_COMMAND_DISCONNECT + SPACE + targetAddress;
            var connectCommand = SDB_PATH + SPACE + SDB_COMMAND_CONNECT + SPACE + targetAddress;
            var devicesCommand = SDB_PATH + SPACE + 'devices';
            var killServerCommand = SDB_PATH + SPACE + SDB_COMMAND_KILL;
            var startServerCommand = SDB_PATH + SPACE + SDB_COMMAND_START;

            if (process.platform !== "win32") {
                console.log(`Not window platfrom need chmod+x sdb shell`);
                innerProcess.execSync(`chmod +x ${SDB_PATH}`);
            }
            console.log(moduleName + 'Prepare to connect your target');

            //When first run the extension , restart sdb to avoid sdb version not compatiable issue
            var extension_state = common.getExtensionState();

            if (extension_state == common.ENUM_EXTENSION_STATE.STOPPED) {
                console.log(moduleName + 'It is first time to run the extension, restart the sdb');
                common.setExtensionState(common.ENUM_EXTENSION_STATE.INITIALIZED);
                //this.restartSdb();
                launchTarget.restartSdb();
            }

            //Disconnect to target device first 
            console.log(moduleName + 'disconnectCommand:' + disconnectCommand);
            innerProcess.exec(disconnectCommand, function (error, stdout, stderr) {
                if (error) {
                    var disconnFailMsg = 'disconnectCommand is failed';
                    common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, disconnFailMsg);
                    console.log(moduleName + `exec error: ${error}`);
                    console.log(moduleName + disconnFailMsg);
                    throw error;
                }
                console.log(moduleName + 'Disconnect your target successful');

                //connect to target device 
                console.log(moduleName + 'connectCommand:' + connectCommand);
                var connectCommandResult = innerProcess.exec(connectCommand, function (error, stdout, stderr) {
                    if (error) {
                        var connFailMsg = 'connectCommand is failed';
                        common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, connFailMsg);
                        console.log(moduleName + `exec error: ${error}`);
                        console.log(moduleName + connFailMsg);
                        throw error;
                    }
                    console.log(moduleName + 'connection result:' + stdout);

                    if (stdout.indexOf('error') >= 0) {
                        var failConnectMsg = 'Failed to connect to target device';
                        common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, failConnectMsg);
                        console.log(moduleName + failConnectMsg);
                        deferred.reject();
                        return;
                    }
                    console.log(moduleName + 'Connected the target');

                    //List devices    
                    console.log(moduleName + 'devicesCommand:' + devicesCommand);
                    if (!launchTarget.getDeviceStatus(targetAddress)) {
                        var cantFindDeviceMsg = 'Cannot find the device in devices list';
                        common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, cantFindDeviceMsg);
                        console.log(moduleName + cantFindDeviceMsg);
                        deferred.reject();
                        return;
                    }
                    console.log(moduleName + 'Found the device');

                    //Cat target version
                    console.log(moduleName + 'Pleae check your target version');
                    var targetAddressArray = targetAddress.split(':');
                    var targetIP = targetAddressArray[0];

                    TARGET_IP = targetIP;

                    SDB_OPT_SERIAL = '-s ' + targetIP;
                    var catCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + SDB_COMMAND_CAT;
                    console.log(moduleName + 'catCommand:' + catCommand);

                    targetVersion = common.getTargetVersion(catCommand);
                    console.log(moduleName + 'targetVersion=' + targetVersion);

                    // Refresh the application.wgt

                    //var buildPackage = require('./buildPackage');
                    //buildPackage.handleCommand();

                    /*if (common.getFuncMode() != common.ENUM_COMMAND_MODE.DEBUGGER) {
                        var buildPackage = require('./buildPackage');
                        buildPackage.handleCommand();
                    }*/

                    deferred.resolve(dirpath);
                });

            });

        } else {
            var noWebApp = 'There is no web app in workspace';
            common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, noWebApp);
            console.log(moduleName + noWebApp);

        }
        return deferred.promise;

    };


    var runApp = function (appPath) {
        console.log(moduleName + ' ================ runApp');

        var pathArray = workspacePath.split(path.sep);

        getAppInfo(appPath, APP_TYPE)
            .then(appName => {
                if (appName === 'undefined') {
                    throw ('Can not get app name');
                }
                outputFullPath = path.normalize(workspacePath + appName + APP_TYPE);

                console.log(moduleName + ' outputFullPath = ' + outputFullPath);

                runAppOnTizen3(appName);
            })



    };

    // Run app on tizen3.0 target
    var runAppOnTizen3 = function (appName) {
        console.log(moduleName + '~~~~~~~~~~~ runAppOnTizen 3.0');
        var configFilePath = workspacePath + path.sep + 'tizen-manifest.xml';
        var appId;// = common.getConfAppID(configFilePath);

        // Get packaged widget path

        //Set root authority
        console.log(moduleName + 'Set the priviledge as root');
        var rootCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + SDB_COMMAND_ROOT;
        innerProcess.execSync(rootCommand);

        //Push wgt to target device 
        console.log(moduleName + 'Trying to push the wgt to target device');
        var localPath = outputFullPath;
        var remotePath = '/opt/usr/home/';//'/opt/usr/home/owner/apps_rw/tmp/';
        //var remotePath = '/home/owner/share/tmp/sdk_tools/tmp/';
        var pushCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + SDB_COMMAND_PUSH + SPACE + localPath + SPACE + remotePath;
        console.log(moduleName + 'pushCommand:' + pushCommand);
        innerProcess.exec(pushCommand, function (error, stdout, stderr) {
            if (error) {
                var failPushMsg = 'Failed to push to widget to target';
                common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, failPushMsg);
                console.log(moduleName + 'exec error: ' + error);
                console.log(moduleName + failPushMsg);
                reject('fail');
                throw error;
            }
            console.log(moduleName + 'Push result:' + stdout);
            console.log(moduleName + 'Pushed the widget target device successfully');

            //var setVconfCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + 'shell 0 setRWIAppID' + SPACE + 'null';
            //If debug mode ,set vconf 
            /*if ((common.getFuncMode() == common.ENUM_COMMAND_MODE.DEBUGGER) || (common.getFuncMode() == common.ENUM_COMMAND_MODE.WEB_INSPECTOR_ON_TV)) {
                setVconfCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + 'shell 0 setRWIAppID' + SPACE + appId;      
            }
            innerProcess.execSync(setVconfCommand);

            //Uninstall package 
            var uninstallCommand =  SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + WAS_COMMAND_UNINSTALL + SPACE + appId;
            innerProcess.execSync(uninstallCommand);*/

            // Install package to target Device                 
            var appNameArray = outputFullPath.split(path.sep);

            //var appName = getAppName();//appNameArray[appNameArray.length - 4];
            appId = appName;
            var installCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + WAS_COMMAD_INSTALL + SPACE + appId + SPACE + remotePath + appName + APP_TYPE;

            // Lanuch App on target device
            var launchCommand = SDB_PATH + SPACE + SDB_OPT_SERIAL + SPACE + WAS_COMMAD_LAUNCH + SPACE + appId;
            installAndLaunch(installCommand, launchCommand);

        });

    };


    //Install and luanch wgt 
    var installAndLaunch = function (installCommand, launchCommand) {
        console.log(moduleName + '================installAndLaunch');
        console.log(`${moduleName} Start to install ${APP_TYPE} to the device`);
        console.log(moduleName + 'installCommand:' + installCommand);
        innerProcess.exec(installCommand, function (error, stdout, stderr) {
            if (error) {
                var installFailMsg = 'Failed install the App';
                common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, installFailMsg);
                console.log(moduleName + `exec error: ${error}`);
                console.log(moduleName + installFailMsg);
                throw error;
            }

            console.log(moduleName + 'Install result:' + stdout);
            if (stdout.indexOf('install failed') >= 0) {
                var failInstallApp = 'Failed install the App, please check';
                common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, failInstallApp);
                console.log(moduleName + failInstallApp);
                return;
            }
            console.log(moduleName + 'Install the wgt successfully');

            // Output JS Log info from target Device 
            console.log(moduleName + 'Start the SDB dlog function ');
            const spawn = require('child_process').spawn;
            //const dlog = spawn(SDB_PATH, ['dlog', '-v', 'time', 'ConsoleMessage']);
            const dlog = spawn(SPAWN_SDB_PATH, ['-s', TARGET_IP, 'shell', '0', 'showlog', 'time']);

            dlog.stdout.on('data', function (data) {
                //console.log(`stdout: ${data}`);
                console.log(moduleName + `stdout: ${data}`);
            });

            dlog.stderr.on('data', function (data) {
                console.log(moduleName + `stderr: ${data}`);
            });

            dlog.on('close', function (code) {
                console.log(moduleName + 'child process exited with code ${code}');
            });


            console.log(moduleName + 'Start to launch the app');
            console.log(moduleName + 'launchCommand:' + launchCommand);
            innerProcess.exec(launchCommand, function (error, stdout, stderr) {
                if (error) {
                    console.log(moduleName + `exec error: ${error}`);
                    throw err;
                }

                console.log(moduleName + 'Launch result:' + stdout);
                if (stdout.indexOf('fail') >= 0) {
                    var launchFailApp = 'Failed to launch the App, please check';
                    console.log(moduleName + launchFailApp);
                    return;
                }
                console.log(moduleName + 'Launch your app successfully');

                // web inspector on tv mode
                if (common.getFuncMode() == common.ENUM_COMMAND_MODE.WEB_INSPECTOR_ON_TV) {
                    var debugIp = TARGET_IP + ':7011';
                    //seqRequest(debugIp, false);
                    var httpRequestCount = 0;
                    launchTarget.seqRequest(httpRequestCount, debugIp, false, targetVersion);
                }
                else {
                    console.log(moduleName + '==============================Run on TV end!');
                }

            });

        });

    };


    return {
        // Handle 'Run on TV' command
        handleCommand: function (launchTargetIP, appPath, type) {

            APP_TYPE = type !== WGT ? TPK : WGT;

            // For getting compatible with sdb in Tizen Studio
            var INSTALLED_SDB_INSDK;

            if (fs.existsSync(INSTALLED_SDB_INSDK)) {
                SPAWN_SDB_PATH = INSTALLED_SDB_INSDK;
                SDB_PATH = '\"' + INSTALLED_SDB_INSDK + '\"';
            } else {
                SPAWN_SDB_PATH = EXTENSION_PATH + path.sep + LIB_PATH + path.sep + SDB_FOLDER + path.sep + SDB_NAME;
                SDB_PATH = '\"' + SPAWN_SDB_PATH + '\"';
            }

            //moduleName = (common.getFuncMode() == common.ENUM_COMMAND_MODE.WEB_INSPECTOR_ON_TV) ? 'Debug on TV' : 'Run on TV';

            moduleName = (common.getFuncMode() == common.ENUM_COMMAND_MODE.WEB_INSPECTOR_ON_TV) ? 'WebInspector on TV' : 'Run on TV';
            console.log(moduleName + ' ============================== Run on TV start!');

            var dirpath = APP_TYPE === TPK ? common.getWorkspacePath() + appPath : appPath;
            var targetip = launchTargetIP;//common.getTargetIp();


            var promise = prepareInstall(dirpath, targetip);
            promise.then(path => runApp(path));
        },


        // Restart sdb to compatiable sdb server version and client version
        restartSdb: function () {

            console.log(moduleName + 'Begin SDB restart');
            var killServerCommand = SDB_PATH + SPACE + 'kill-server';
            var startServerCommand = SDB_PATH + SPACE + 'start-server';

            //Kill Server to compatiable with client sdb version 
            console.log(moduleName + 'killServerCommand: ' + killServerCommand);
            try {

                var killServerResult = innerProcess.execSync(killServerCommand);
                console.log(moduleName + 'Kill completed: ' + killServerResult.toString());
            } catch (ex) {

                console.log(moduleName + 'Failed to kill exsiting SDB process');
                console.log(moduleName + ex.message);
            }

            // Start SDB again
            console.log(moduleName + 'startServerCommand: ' + startServerCommand);
            innerProcess.exec(startServerCommand, function (error, stdout, stderr) {

                if (error) {
                    console.log(moduleName + `Failed to start SDB server: ${error}`);
                    throw error;
                }

                console.log(moduleName + 'Started SDB server: ' + stdout);
            });
            common.sleepMs(2000);

        },


        // Send HTTP request to target device to get the TV debug webInspector URL
        seqRequest: function (httpRequestCount, debugIp, flag, targetversion) {

            console.log(moduleName + 'flag:' + flag);
            console.log(moduleName + 'Remote WebInspector debugIp:' + debugIp);
            httpRequestCount = httpRequestCount + 1;
            var aUrl = 'http://' + debugIp + '/pagelist.json';
            var webInspectorValue = ''; //web inspector value in pagelist.json or json page
            if (targetversion == '3.0') {
                aUrl = 'http://' + debugIp + '/json';
            }

            console.log(moduleName + 'Remote WebInspector requestUrl:' + aUrl);
            var timeoutEventId;
            var req = http.get(aUrl, function (res) {

                var resultdata = '';
                res.on('data', function (chunk) {
                    resultdata += chunk;
                });

                res.on('end', function () {
                    if (res.statusCode != 200) {
                        console.log(moduleName + '================Resend Request to get Json data============= ');
                        launchTarget.seqRequest(httpRequestCount, debugIp, flag, targetversion);
                    } else {
                        console.log(moduleName + '================Request to get Json data successfully============== ');
                        var responseArray = JSON.parse(resultdata);
                        var pages = responseArray.filter(function (target) { return target });
                        console.log(moduleName + 'WebInspectorURl.pages.length:' + pages.length);

                        if (pages.length > 0 && targetversion == '3.0') {
                            webInspectorValue = pages[0].devtoolsFrontendUrl;
                        } else if (pages.length > 0 && targetversion == '2.4') {
                            webInspectorValue = pages[0].inspectorUrl;
                        }

                        if (webInspectorValue && (webInspectorValue != '')) {


                            console.log(moduleName + 'WebInspectorURl.pages.inspectorUrl:' + webInspectorValue);
                            if ((common.getFuncMode() == common.ENUM_COMMAND_MODE.WEB_INSPECTOR_ON_TV) || (common.getFuncMode() == common.ENUM_COMMAND_MODE.WEB_INSPECTOR_ON_EMULATOR)) {
                                //var vscode = require('vscode');						
                                //var chromeExceutePath = vscode.workspace.getConfiguration('tizentv')['chromeExecutable'];
                                console.log(moduleName + 'Finding configured chromeExceutePath:' + chromeExceutePath);
                                if (chromeExceutePath == null || typeof (chromeExceutePath) == 'undefined') {
                                    var chromeNotFound = 'Chrome exceutable file not found, please check and set the default path in user setting';
                                    common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, chromeNotFound);
                                    console.log(moduleName + chromeNotFound);
                                    return;
                                }

                                if (!fs.existsSync(chromeExceutePath)) {
                                    var nullPathMsg = "Cannot find the Chrome Executable, please configure it!";
                                    common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.WARNING, nullPathMsg);
                                    console.log(moduleName + nullPathMsg);
                                    //return;

                                    if (process.platform == 'linux') {
                                        chromeExceutePath = '/opt/google/chrome/google-chrome';
                                    } else if (process.platform == 'win32') {
                                        chromeExceutePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
                                    } else if (process.platform == 'darwin') {
                                        chromeExceutePath = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
                                    }
                                }


                                var WebInspectorUrl = 'http://' + debugIp + webInspectorValue;
                                console.log(moduleName + 'WebInspectorUrl:' + WebInspectorUrl);

                                var startChromeCommand = '';

                                if (process.platform == 'win32') {
                                    startChromeCommand = '\"' + chromeExceutePath + '\"' + SPACE + '\"' + WebInspectorUrl + '\"';
                                }
                                else {
                                    startChromeCommand = '\"' + chromeExceutePath + '\"' + SPACE + WebInspectorUrl;
                                }
                                // Launch the Web Inspector URL page in the chrome browser debugger
                                console.log(moduleName + 'startChromeCommand:' + startChromeCommand);
                                innerProcess.exec(startChromeCommand, function (error, stdout, stderr) {
                                    if (error) {
                                        console.log(moduleName + `exec error: ${error}`);
                                        throw error;
                                    }
                                });



                                flag = true;
                            } else {
                                var modeErrMsg = "Google chrome's web inspector is not supported in current Tizen platform debug mode";
                                common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, modeErrMsg);
                                console.log(moduleName + modeErrMsg);
                                return;
                            }

                        } else {
                            launchTarget.seqRequest(httpRequestCount, debugIp, flag, targetversion);
                        }

                    }
                });
            });

            req.on('error', function (err) {
                console.log(moduleName + 'Failed to send request: ' + err.message);
                common.sleepMs(2000);
                if (!flag) {
                    if (httpRequestCount < 5) {
                        console.log(moduleName + 'Repeat request: ' + httpRequestCount + ' time, request 5 times for maxmimum');
                        launchTarget.seqRequest(httpRequestCount, debugIp, flag, targetversion);
                    } else {
                        var requestFailMsg = 'Failed to send request: ' + err.message + ', please try later';
                        common.showMsgOnWindow(common.ENUM_WINMSG_LEVEL.ERROR, requestFailMsg);
                        console.log(moduleName + requestFailMsg);
                    }

                }

            });


            req.on('timeout', function (err) {
                console.log(moduleName + 'Http Request timeout, abort connection to Remote WebInspector!');
                req.abort();
            });

            timeoutEventId = setTimeout(function () {
                req.emit('timeout', { message: 'have been timeout...' });
            }, 5000);
        },

        // Handle 'Debug on TV 3.0' command
        prepareInstallForDebug: function (dirPath, targetIp) {
            console.log(moduleName + '============================== prepare Install For Launch');
            SPAWN_SDB_PATH = EXTENSION_PATH + path.sep + LIB_PATH + path.sep + SDB_FOLDER + path.sep + SDB_NAME;
            SDB_PATH = '\"' + SPAWN_SDB_PATH + '\"';
            workspacePath = dirPath;
            SDB_OPT_SERIAL = '-s ' + targetIp;
            targetVersion = '5.0';
            TARGET_IP = targetIp;
            runApp();
        },

        //Get the device status by sdb devices command 
        getDeviceStatus: function (targetAddress) {
            console.log(moduleName + '================getDeviceStatus');
            //List devices    
            var devicesCommand = SDB_PATH + SPACE + 'devices';

            //console.log('devicesCommand:'+devicesCommand);      
            var listdata = innerProcess.execSync(devicesCommand);
            console.log(moduleName + 'devices result:' + listdata.toString());

            if (listdata.indexOf(targetAddress) < 0) {
                return false;
            } else {
                return true;
            }

        },

        // Get the current extension dir path 
        getExtensionPath: function () {
            console.log(moduleName + '================getExtensionPath');
            return EXTENSION_PATH;
        }

    };


})();
module.exports = launchTarget;