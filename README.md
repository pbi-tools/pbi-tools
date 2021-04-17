# pbi-tools-cli

Introduction

## User Notes

## Usage

See [detailed CLI docs here](./docs/Usage.md).

### Command-line Syntax

    -argumentName argumentValue
    /argumentName:argumentValue
    -argumentName                   - If the argument is a boolean it will be true in this case.

### Diagnostics

* Log output can be controlled using the environment variable `PBITOOLS_LogLevel`.
* Allowed values are:
  - Verbose
  - Debug
  - Information
  - Warning
  - Error
  - Fatal
* The default is ***Information***, which is also effective when an unknown/invalid option has been specified.

## Developer Notes

### Build System

* All build targets are implemented using [FAKE](https://fake.build/).
* Dependencies are managed using [Paket](https://fsprojects.github.io/Paket/).
* Main entry point for all build tasks is `.\build.cmd`.
* The [fake-cli](https://fake.build/fake-commandline.html) tool is installed as a [local .NET Core Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools#install-a-local-tool), see `.\.config\dotnet-tools.json`. That's why a .Net Core 3.x SDK is required to build the project. The `build.cmd` script handles the tool installation.

### Prerequisites

* Visual Studio 2019 (for MSBuild dependencies)
* .Net Core SDK 3.x
* Power BI Desktop x64 (Must be installed in default location for local development: `C:\Program Files\Microsoft Power BI Desktop\`)

### Versioning

### Run Tests

    .\build.cmd Test

### Install Dependencies

    dotnet paket install

### Update Dependency to latest version (ex: AMO)

    dotnet paket update Microsoft.AnalysisServices.retail.amd64
    dotnet paket update Microsoft.AnalysisServices.AdomdClient.retail.amd64

### Updating Build Dependencies

    dotnet paket update -g Fake-Build

### Find outdated dependencies

    dotnet paket outdated -g Main

### Invoke Build script directly

    dotnet fake {...}
    dotnet fake -t Build
    dotnet fake --version

### Build

    .\build.cmd Build

### Extract embedded sample PBIX with local build version and using default settings

    .\pbi-tools.local.cmd extract '.\data\Samples\Adventure Works DW 2020.pbix'

### Extract embedded sample PBIX with local build version and 'Raw' serialization mode, into custom output folder

    .\pbi-tools.local.cmd extract '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw
