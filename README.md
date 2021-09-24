# MangoSeed
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/MangoSeed?label=CLI&style=for-the-badge) ![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/MangoSeed.Core?color=green&label=Library&style=for-the-badge)

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/7efb6e80c6a1438e983fb369a9551f8f)](https://app.codacy.com/gh/giometrix/MangoSeed?utm_source=github.com&utm_medium=referral&utm_content=giometrix/MangoSeed&utm_campaign=Badge_Grade_Settings)
[![Build status](https://ci.appveyor.com/api/projects/status/uc792o6b8l1ujiom?svg=true)](https://ci.appveyor.com/project/giometrix/mangoseed)


A library/command line utility (that can be installed a [.net tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools)) to import/export data from MongoDb.

The tool is similar to MongoImport/Export, but 
1. can be used as a library, which may be easier to use in test scenarios
2. has options for parallel file imports
3. is written in .net, making it easier to customize for a .net developer to customize how the tool works

## Command Line Usage
_MangoSeed_ is a cross platform utility (Linux, Windows, OSX) that can be run stand-alone or be installed as a .net tool.  The easiest option is to install it as a .net tool (no fuss, easy updating, etc).

### Installation
To install _MangeSeed_ as a .net global tool, run the following command:
```bash
dotnet tool install --global MangoSeed
```
You can also install _MangoSeed_ as a local tool:
```bash
dotnet new tool-manifest # if you are setting up this repo
dotnet tool install --local MangoSeed
```

:pencil2: **Note**

If you are using zsh, the dotnet tool installer does not add it's tools directory to your paths.  You can resolve this by adding the following to ~/.zshrc
```bash
export PATH=$HOME/.dotnet/tools:$PATH
```

### Usage
_MangoSeed_ has 2 high level capabilities, importing and exporting.

To view all of the import options:
```
mangoseed import --help
```

To view all of the export options:
```
mangoseed export --help
```
