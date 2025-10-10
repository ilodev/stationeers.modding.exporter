# stationeers.modding.exporter

Exporter package to create assetbundles for Stationeers that work with StationeersLaunchPad.

## Installation

Use Unity's Package Manager via git URL:
- Open Unity Package Manager.
- Click on the + sign and select Add Package from Git URL.
- Use the git URL: https://github.com/ilodev/stationeers.modding.exporter.git


Use Unity's Package Manager via local path:
- Open Unity Package Manager.
- Click on the + sign and select Add Package from disk.
- Select the package.json file in the package folder.

## Dependencies

Installing this package will automatically install: 

 - com.unity.mathematics 1.2.6
 - com.unity.collections 2.2.1
 - com.unity.textmeshpro 3.0.6
 - com.unity.ugui 1.0.0

Due to limitations installing dependencies from Git URLs, UniTask 
is not installed as a package and the game dlls have been added 
to the package instead.

Mono.Cecil is required by BepInEx and Harmony and is installed with 
the Collections package.

## Version

Version 1.0.1

## Credits

- Stationeers Modding Team - [Join our discord](https://discord.gg/5qZbPVTw2U)


## Notes 

- Temporarily we will still use the StationeersMods exporter.
- To reduce the package dependencies this one includes the version of Mono.Cecil required.

