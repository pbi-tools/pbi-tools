# pbi-tools

`pbi-tools` is a command-line tool bringing source-control features to Power BI. It works alongside Power BI Desktop and enables mature enterprise workflows for Power BI projects.

An example project is available here: <https://github.com/pbi-tools/adventureworksdw2020-pbix>

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/pbi-tools/pbi-tools)](https://github.com/pbi-tools/pbi-tools/releases/latest)
[![Twitter Follow](https://img.shields.io/twitter/follow/mthierba)](https://twitter.com/mthierba) [![Join the chat at https://gitter.im/pbi-tools/general](https://badges.gitter.im/pbi-tools/general.svg)](https://gitter.im/pbi-tools/general?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

- Twitter Hashtag: [#pbitools](https://twitter.com/search?q=%23pbitools&src=typed_query)

## User Notes

- See <https://pbi.tools/cli/>

## Developer Notes

### Build System

- All build targets are implemented using [FAKE](https://fake.build/).
- Dependencies are managed using [Paket](https://fsprojects.github.io/Paket/).
- Main entry point for all build tasks is `.\build.cmd`.
- The [fake-cli](https://fake.build/fake-commandline.html) tool is installed as a [local .NET Core Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools#install-a-local-tool), see [.config\dotnet-tools.json](./.config/dotnet-tools.json). The `build.cmd` script handles the tool installation.

### Prerequisites

- Visual Studio 2019 or later (for MSBuild dependencies)
- .Net 4.7.2 Targeting Pack
- .Net 6.0 SDK
- Power BI Desktop x64 (Must be installed in default location for local development: `C:\Program Files\Microsoft Power BI Desktop\`)

### List Build Targets

    dotnet fake build --list

### Versioning

The project strictly adheres to [SemVer v2](https://semver.org/) for release versioning. The build system uses the first entry in [RELEASE_NOTES.md](./RELEASE_NOTES.md) to inject version numbers into build artifacts.

### Diagnostics

- Log output can be controlled using the environment variable `PBITOOLS_LogLevel`.
- Allowed values are:
  - Verbose
  - Debug
  - Information
  - Warning
  - Error
  - Fatal
- The default is ***Information***, which is also effective when an unknown/invalid option has been specified.

### Build

    .\build.cmd Build

### Run Tests

    .\build.cmd Test

### Run All Targets (Build, Publish, Test, UsageDocs, Pack)

    .\build.cmd Pack

### Run only the specified build target

    .\build.cmd UsageDocs -s
    dotnet fake build -s -t SmokeTest

### Install Dependencies

    dotnet paket install

_That is generally not needed as the `build.cmd` script takes care of fetching dependencies. However, it could be useful to run this manually on a fresh clone or after making changes in the `paket.dependencies` file._

### Update Specific Dependency to latest version (ex: AMO)

    dotnet paket update Microsoft.AnalysisServices.retail.amd64
    dotnet paket update Microsoft.AnalysisServices.AdomdClient.retail.amd64

### Updating All Dependencies (NuGet)

    dotnet paket update
    dotnet paket update -g Fake-Build

### Find outdated dependencies

    dotnet paket outdated -g Main

### Invoke Build script directly

    dotnet fake {...}
    dotnet fake -t Build
    dotnet fake --version

### Extract embedded sample PBIX with local build version and using default settings

    .\pbi-tools.local.cmd extract '.\data\Samples\Adventure Works DW 2020.pbix'

### Extract embedded sample PBIX with local build version and 'Raw' serialization mode, into custom output folder

    .\pbi-tools.local.cmd extract '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw

### Enable Debug logging (PowerShell)

    $env:PBITOOLS_LogLevel = "Debug"

### Fast local build (no clean)

    .\build.cmd Publish -s
    .\build.cmd Pack -s

## Git Submodules

### Clone with submodules

   git clone --recurse-submodules https://github.com/pbi-tools/pbi-tools.git

### Pulling in Upstream Changes

    git submodule update --remote

### Clone specific single branch into named folder, with submodules

    git clone -b Release/1.0.0-beta.9 --single-branch --recurse-submodules https://github.com/pbi-tools/pbi-tools.git ./1.0.0-beta.9
