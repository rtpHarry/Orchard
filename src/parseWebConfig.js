// assumes there isn't a <remove> tag in the appSettings

var fs = require('fs'),
	glob = require('glob'),
    xml2js = require('xml2js');

//function getAssetGroups() {
//}

var assetsConfig = require("./assetsConfig.json")[0];
// TODO: key's may not exist in cfg file
if(assetsConfig.config.importCustomModulePaths || assetsConfig.config.importCustomThemePaths) {
	var parser = new xml2js.Parser();
	fs.readFile(__dirname + '/Orchard.Web/web.config', function (err, data) {
		parser.parseString(data, function (err, result) {
			var appSettings = result.configuration.appSettings[0].add;
			var foundKeys = [];			
			if(assetsConfig.config.importCustomModulePaths) {
				foundKeys.push(searchForAppSetting("Modules", appSettings));
			}
			if(assetsConfig.config.importCustomThemePaths) {
				foundKeys.push(searchForAppSetting("Themes", appSettings));
			}
			var paths = getSplitPaths(foundKeys);
			paths.push("Orchard.Web/{Core,Modules,Themes}/*/Assets.json");
			
			assetManifestPaths = getAssetManifestPaths(paths);
			console.dir(assetManifestPaths); 
		});
	});
}
// if (get modules or themes)
	// run web.config loader
	// call getAssetManifestPaths
// just pass to getAssetManifestPaths 

function searchForAppSetting (key, appSettings) {
	var i = null;
	for (i = 0; appSettings.length > i; i += 1) {
		if (appSettings[i].$.key === key) {
			return appSettings[i].$.value;
		}
	}
	return '';
};

function getSplitPaths(foundKeys) {
	var output = [];
	for (i = 0; foundKeys.length > i; i += 1) {
		var key = foundKeys[i].split(',').map(function (input){
			output.push(convertPathToGlob(input));
		});		
	}	
	return output;
}

function convertPathToGlob(inputPath){
	inputPath = inputPath.trim().replace(/^\~/, '/Orchard.Web');
	inputPath = inputPath.endsWith('/') ? inputPath : inputPath + '/'
	inputPath = inputPath + "*/Assets.json";	
	return inputPath;
}

function getAssetManifestPaths(globs) {
	var assetManifestPaths = [];
	for (i = 0; globs.length > i; i += 1) {
		assetManifestPaths = assetManifestPaths.concat(glob.sync(globs[i]));
	}
	return assetManifestPaths;
}