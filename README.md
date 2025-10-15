# stationeers.modding.exporter

Exporter package to create assetbundles for Stationeers to be loaded by [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad).

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

No dependencies are known for this package.


## Usage

Exporting now is integrated with the Unity building system. You will 'Build' the export through the 
File > Build settings (Control+Shift+b) or Build and Run (Control+b). There is no dedicated 'Export Mod'
button anymore. 

The first time you select a build mode (e.g. Clean build, normal Build, Skip data build or Build and Run), 
Unity will ask you for the location of the export folder (usually  Documents/My Games/Stationeers/mods/ folder). 
From that moment, just hitting Control+B will export the mod again to the same location. 

Exporting/Building the project makes use of the Unity Player Settings (including Company Name, Product Name
and Bundle Version). The exporter package will keep the Player Settings and the Assets/About/About.xml files
synchronized, becuase both share the same information. You can edit the About.xml file and the Player Settings
will be updated or viceversa.

The current Tools menu is temporary and contains individual test functions for the exporting process.

## Version

Version 1.0.1

## Credits

- Stationeers Modding Team - [Join our discord](https://discord.gg/5qZbPVTw2U)