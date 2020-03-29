# pbi-tools-cli

## Build System

* All build targets are implemented using [FAKE](https://fake.build/).
* Dependencies are managed using [Paket](https://fsprojects.github.io/Paket/).
* Main entry point for all build tasks is `.\build.cmd`.
* The [fake-cli](https://fake.build/fake-commandline.html) tool is installed as a [local .NET Core Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools#install-a-local-tool), see `.\.config\dotnet-tools.json`. That's why a .Net Core 3.x SDK is required to build the project. The `build.cmd` script handles the tool installation.

### Prerequisites

* Visual Studio 2019
* .Net Core SDK 3.x
* Power BI Desktop x64 (Installed in default location: `C:\Program Files\Microsoft Power BI Desktop\`)

#### Run Tests

    .\build.cmd Test

#### Install Dependencies

    .\.paket\paket install

#### Invoke Build script directly

    dotnet fake {...}
    dotnet fake -t Build

### Build

    .\build.cmd Build

## Diagnostics

* Log output can be controlled using the environment variable `PBITOOLS_LogLevel`. Allowed values are: *Verbose, Debug, Information, Warning, Error, Fatal*. The default is ***Information***, which is also effective when an unknown option has been specified.
