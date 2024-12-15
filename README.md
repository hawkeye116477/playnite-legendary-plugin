# playnite-legendary-plugin
Epic Games Store library integration with [Legendary Launcher](https://github.com/derrod/legendary#prebuilt-standalone-binary-windows-macos-and-linux) for [Playnite](https://github.com/JosefNemec/Playnite), based on [Epic Store library integration](https://github.com/JosefNemec/PlayniteExtensions/tree/master/source/Libraries/EpicLibrary).

[![Crowdin](https://badges.crowdin.net/playnite-legendary-plugin/localized.svg)](https://crowdin.com/project/playnite-legendary-plugin)
[![GitHub release (latest by date)](https://img.shields.io/github/downloads/hawkeye116477/playnite-legendary-plugin/latest/total)](https://github.com/hawkeye116477/playnite-legendary-plugin/releases/latest)

## **Bugs**
If you encounter any bug, then you can report it at [github.com/hawkeye116477/playnite-legendary-plugin/issues](https://github.com/hawkeye116477/playnite-legendary-plugin/issues), but before opening any ticket you should read [Troubleshooting section on wiki](https://github.com/hawkeye116477/playnite-legendary-plugin/wiki/Troubleshooting).

## **New cool features**
If you want some new feature, then you can say about that at [https://github.com/hawkeye116477/playnite-legendary-plugin/issues](https://github.com/hawkeye116477/playnite-legendary-plugin/issues/new?assignees=&labels=enhancement&projects=&template=features.yml), but before opening any new subject you should look at [github.com/users/hawkeye116477/projects/1](https://github.com/users/hawkeye116477/projects/1) if it's not already planned.

## **Questions**
If you read [wiki](https://github.com/hawkeye116477/playnite-legendary-plugin/wiki) and still don't know something, then you can ask a question on [forum](https://github.com/hawkeye116477/playnite-legendary-plugin/discussions).

## **Building**
To build this extension, for first you need to clone that repo and [CliWrap](https://github.com/hawkeye116477/CliWrap) in same directory. Then you can use **Visual Studio 2022 Community** to buid **CliWrap** and then plugin (for plugin you can use **VS 2019** to see preview of GUI, but **CliWrap** requires **2022**). You may also need to replace version number in `extension.yaml` file or download latest **Python**, so version number will follow `AssemblyInfo.cs` file. After you compile both, you can open `Playnite => Settings => For developers` and choose path where is dll located to load it or alternatively go to `make_scripts` directory and execute `make_extension.py` script with **Python** to make single .pext file which can be dropped to Playnite's window to install it.

## **License**
This project is distributed under the terms of the [MIT license](/LICENSE) and uses third-party libraries that are distributed under their own terms (see [ThirdPartyLicenses.txt](/ThirdPartyLicenses.txt)).
