/**
* author: zhaofeng.vip@gmail.com
*
 */
'use strict';

var TizenSDK = {
    get buildPackage() { return require('./buildPackage'); },
    get launchTarget() { return require('./launchTarget'); },
    get appInfo() { return require('./getAppInfo') }
};

module.exports = TizenSDK;