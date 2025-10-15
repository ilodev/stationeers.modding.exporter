# Changelog

All notable changes to this package will be documented in this file. The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)


## [1.0.1] - 2025-10-08

### Improved exporter

Removed stationeersMods dependency.
Prompt user to save before building/exporting.
Moved towards an process agnostic incremental exporter.
Exporting integrated with the build system through Build Settings: Control+b, control+shift+b
Added option to find/run the game after exporting.
Added option to rebuild the export completely, overwrite new items or update only the dlls.
Integrated Unity Developer Mode from Build Settings.
Integrated Unity Copy PDB files from Build Settings.
Using Player Settings for project/author/version.
Bumped version.

## [1.0.0] - 2025-10-08

### Added

First release, including:
  - Stationeers required Assemblies from version: 0.2.5919.26060 23/09/2025
  - BepInEx version: 5.4.23.2
  - Harmony version: 2.9.0
  - LaunchPadBooster: 0.1.4
