# SM64RM Plugin Collection

A collection of plugins for [Super Mario 64 ROM Manager](https://gitlab.com/Pilzinsel64/sm64-rom-manager), mostly to improve user experience.

## Plugins
For more detailed explanations and a list of dependencies, check the respective plugin's README.
- **ASMError**: Adds an error log to the assembler window.
- **CrystalFix**: Allows using vertex colors with materials that have the "crystal" effect.

More will be added at some point!

## Installation
To install these plugins, download the latest `.zip` archive and extract it. Inside there are `.dll` files corresponding to each plugin.

Navigate to your ROM Manager installation's file path. Inside the `Data` folder there is a `Plugins` folder. Choose the plugins you want and drag them into that folder.

Many plugins also have extra dependencies. To properly install those dependencies, drop their respective `.dll` files into `Data/Libs`. If an error is caused, make sure your `.dll` file is for **.NET 4.8** (usually labelled as `net48`).

## Building from source

If you're looking to compile these plugins for yourself, make sure you have all the correct dependencies. The required libraries are listed in the plugin's README, but all of them require .NET 4.8 and the .NET SDK to compile.
Any required libraries should be placed in the same folder as the plugin you wish to compile, but not within that plugin's folder.