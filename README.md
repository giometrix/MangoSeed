# MangoSeed
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
dotnet tool install --global MangoSeed --version 1.0.0
```
You can also install _MangoSeed_ as a local tool:
```bash
dotnet new tool-manifest # if you are setting up this repo
dotnet tool install --local MangoSeed --version 1.0.0
```

:pencil2: **Note**

If you using zsh, dotnet tool does not add it's director to your paths.  You can resolve this by adding the following to ~/.zshrc
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
