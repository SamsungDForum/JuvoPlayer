// Imports
var os = require('os');
var fs = require('fs');
var path = require('path');
var innerProcessBuild = require('child_process');
var signPackage = require('./signPackage');
var p12ToPem = require('./p12ToPem');
// 3rd packager tool
var archiver = require('archiver');

const getAppInfo = require('./getAppInfo');

var buildPackage = (function () {

	// tizentv extension's path
	var extensionPath = __dirname;
	var outputPath = '/../';

	// Generated personal/public signature file
	var AUTOR_SIGNATURE = 'author-signature.xml';
	var PUBLIC_SIGNATURE = 'signature1.xml';
	var PUBLIC_SIGNATURE2 = 'signature2.xml';
	var MAINFEST_TMP = '.manifest.tmp';

	// default packing type
	var WGT = '.wgt';
	var TPK = '.tpk';
	var APP_TYPE = WGT;

	// Blank
	var BLANK_SPACE = ' ';
	// Module name
	var moduleName = 'Build Package';
	var workspacePath = '';


	// Remove exsiting files
	function removeFile(filepath) {
		if (fs.existsSync(filepath)) {

			console.log(`${moduleName} The existing ${filepath} will be removed firstly`);
			console.log(`${moduleName} Remove existing ${filepath}`);

			try {

				fs.unlinkSync(filepath);
			} catch (ex) {

				console.log(`${moduleName} The existing ${filepath} cannot be removed`);
				console.log(`${moduleName} ex.message`);
				return false;
			}
		}
	}

	// Validate the path
	// Do App signature
	var prePackage = function (workspacePath, appName) {
		// Remove exsiting packager or signature1.xml or author-signature.xml 
		var exsitingPackager = workspacePath + path.sep + appName + WGT;
		var exsitingPackagerMore = workspacePath + path.sep + appName + TPK;
		var exsitingAuthorSignatureXML = workspacePath + path.sep + AUTOR_SIGNATURE;
		var exsitingSignature1XML = workspacePath + path.sep + PUBLIC_SIGNATURE;
		var existingSignature2XML = workspacePath + path.sep + PUBLIC_SIGNATURE2;
		removeFile(exsitingPackager);
		removeFile(exsitingPackagerMore);
		removeFile(exsitingAuthorSignatureXML);
		removeFile(exsitingSignature1XML);
		removeFile(existingSignature2XML);

		//Remove existing active_cert pem files in Developer and Distributor
		removeFile(p12ToPem.ACTIVE_PEM_FILE.AUTHOR_KEY_FILE);
		removeFile(p12ToPem.ACTIVE_PEM_FILE.AUTHOR_CERT_FILE);
		removeFile(p12ToPem.ACTIVE_PEM_FILE.DISTRIBUTOR_KEY_FILE);
		removeFile(p12ToPem.ACTIVE_PEM_FILE.DISTRIBUTOR_CERT_FILE);
		removeFile(p12ToPem.ACTIVE_PEM_FILE.DISTRIBUTOR2_KEY_FILE);
		removeFile(p12ToPem.ACTIVE_PEM_FILE.DISTRIBUTOR2_CERT_FILE);

		//signature a package which use crypto nodejs library
		try {

			console.log(`${moduleName} : Signing app, please wait...`);
			//signPackage.signPackage();
			signPackage.signPackage(workspacePath);
			console.log(`${moduleName} : Completed sign...`);
		} catch (ex) {

			console.log(`${moduleName} : Do application signature failed, please check your environment`);
			console.log(`${moduleName} : xmldom is suggested for signature package`);
			console.log(`${moduleName} : ${ex.message}`);
			return false;
		}

		return true;
	};

	// Do build package with 'archiver'
	var doPackage = function (workspacePath, appName) {

		// Get Web App .wgt file default output path
		var outputFullPathTmp = workspacePath + outputPath + appName + APP_TYPE;
		var outputFullPath = workspacePath + path.sep + appName + APP_TYPE;
		console.log(`${moduleName} : Output put has been set as: ${outputFullPath}`);

		var output = fs.createWriteStream(outputFullPathTmp);
		var archive = archiver('zip');

		archive.on('error', function (err) {
			console.log(`${moduleName} : ${err.message}`);
			throw err;
		});

		output.on('close', function () {

			// Remove tempory signature files
			var authorSignature = workspacePath + path.sep + AUTOR_SIGNATURE;
			var publicSignature = workspacePath + path.sep + PUBLIC_SIGNATURE;
			var publicSignature2 = workspacePath + path.sep + PUBLIC_SIGNATURE2;
			var tmpFile = workspacePath + path.sep + MAINFEST_TMP;
			if (fs.existsSync(authorSignature)) {
				fs.unlinkSync(authorSignature);
			}
			if (fs.existsSync(publicSignature)) {
				fs.unlinkSync(publicSignature);
			}
			if (fs.existsSync(publicSignature2)) {
				fs.unlinkSync(publicSignature2);
			}
			if (fs.existsSync(tmpFile)) {
				fs.unlinkSync(tmpFile);
			}
			fs.rename(outputFullPathTmp, outputFullPath);
			console.log(`${moduleName} : After build package, signature tempory files were removed`);
			console.log(`${moduleName} : ==============================Build Package end!`);
		});

		archive.pipe(output);
		archive.bulk([
			{
				src: ['**'],
				dest: '/',
				cwd: path.join(workspacePath, '/'),
				expand: true
			}
		]);
		archive.finalize();

		// Move .wgt file to App path

		console.log(`${moduleName} : Move ${APP_TYPE} from tempory path`);

		// Complete the package build
		//while (!fs.existsSync(outputFullPath)) {
		//common.sleepMs(500);
		//}
		console.log(`${moduleName} : Generated the ${APP_TYPE} achiver`);
		var buildSuccessMsg = 'Build the package Successfully!';
		console.log(`${moduleName} : ${buildSuccessMsg}`);
	};


	return {
		// Do 'Build Package' command
		// Also invoked by launch App functions
		handleCommand: function (appPath, type) {

			APP_TYPE = type !== WGT ? TPK : WGT;
			console.log(`${moduleName} : ----- Build Package start! ---------`);

			//var workspacePath = common.getWorkspacePath();
			/*if (common.getFuncMode() != common.ENUM_COMMAND_MODE.DEBUGGER && common.getFuncMode() != common.ENUM_COMMAND_MODE.DEBUGGER_TIZEN3_0_EMULATOR) {
				logger.debug(moduleName, 'If is debug mode ,set workspace to current work dir');
				workspacePath = common.getWorkspacePath();
			}*/

			workspacePath = appPath;// + '/bin/packaging';
			console.log(`workspacePath: ${workspacePath}`);
			// Check if there's workspace
			if (typeof (workspacePath) == 'undefined') {
				var noWorkspace = 'No project in workspace, please check!';
				console.log(`${moduleName} : ${noWorkspace}`);
				return;
			}

			// Get App's name
			var pathArray = appPath.split(path.sep);//workspacePath.split(path.sep);
			var appName = pathArray[pathArray.length - 1];

			console.log(`${moduleName} : The app's path is: ${workspacePath}`);

			getAppInfo(appPath, APP_TYPE)
				.then(wgtName => {
					console.log(`${moduleName} : The app's name is: ${wgtName}`);

					if (wgtName == '') {
						var warning_path = 'The input workspace is a invalid, please check if it is a root!';
						console.log(`${moduleName} : ${warning_path}`);
						return;
					}

					if (workspacePath && prePackage(workspacePath, wgtName)) {

						// Package
						doPackage(workspacePath, wgtName);
					}
					else {

						// Show error to users
						var errorMsg = 'Failed to build package!';
						console.log(`${moduleName} : ${errorMsg}`);
					}

				});
		},

		// Handle 'Debug on TV 3.0' command
		prepareBuildForDebug: function (dirpath) {

			console.log(`${moduleName} : ==============================Build package for debug!`);

			workspacePath = dirpath;
			buildPackage.handleCommand();

		}

	};
})();

module.exports = buildPackage;
