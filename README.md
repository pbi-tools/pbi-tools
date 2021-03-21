# pbi-tools-cli

Introduction

## User Notes

## Usage

    pbi-tools <action> -options

_pbi-tools-cli, 1.0.0-beta.2_

### Actions

#### extract

    extract <pbixPath> [<extractFolder>] [<mode>] [<modelSerialization>] 

Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| pbixPath* |  |  | The path to an existing PBIX file. |
| extractFolder |  |  | The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory. |
| mode | `Auto` |  | The extraction mode. <br> `Auto`  - Attempts extraction using the V3 model, and falls back to Legacy mode in case the PBIX file does not have V3 format. <br> `V3`  - Extracts V3 PBIX files only. Fails if the file provided has a legacy format. <br> `Legacy`  - Extracts legacy PBIX files only. Fails if the file provided has the V3 format. |
| modelSerialization |  |  | The model serialization mode. <br> `Default`  - Serializes the tabular model into a standard folder structure and performs various transformations to optimize file contents for source control. <br> `Raw`  - Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. |

**Extract: Custom folder and settings**

    pbi-tools.exe extract '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw

_Extracts the PBIX file into the specified extraction folder (relative to the current working dir), using the 'Auto' compatibility mode. The model part is serialialized using Raw mode._

**Extract: Default**

    pbi-tools.exe extract '.\data\Samples\Adventure Works DW 2020.pbix'

_Extracts the specified PBIX file into the default extraction folder (relative to the PBIX file location), using the 'Auto' compatibility mode. Any settings specified in the '.pbixproj.json' file already present in the destination folder will be honored._

#### extract-data

    extract-data [<port>] <pbixPath> [<outPath>] [<dateTimeFormat>] 

Extract data from all tables in a tabular model, either from within a PBIX file, or from a live session.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| port |  |  | The port number of a local Tabular Server instance. |
| pbixPath* |  |  | The PBIX file to extract data from. |
| outPath |  |  | The output directory. Uses working directory if not provided. |
| dateTimeFormat | `s` |  | The format to use for DateTime values. Must be a valid .Net format string. |

**Extract data from local workspace instance**

    pbi-tools.exe extract-data -port 12345

_Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

**Extract data from offline PBIX file**

    pbi-tools.exe extract-data -pbixPath '.\data\Samples\Adventure Works DW 2020.pbix'

_Extracts all records from each table from the model embedded in the specified PBIX file. Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

#### export-bim

    export-bim <folder> [<skipDataSources>] [<transforms>] 

Converts the Model artifacts to a TMSL/BIM file.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to export the BIM file from. |
| skipDataSources |  | X | Do not generate model data sources. The is required for deployment to Power BI Premium via the XMLA endpoint. |
| transforms |  |  | List transformations to be applied to TMSL document. <br> `RemovePBIDataSourceVersion`  - Removes the 'defaultPowerBIDataSourceVersion' model property, making the exported BIM file compatible with Azure Analysis Services. |

#### info

    info [<checkDownloadVersion>] 

Collects diagnostic information about the local system and writes a JSON object to StdOut.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| checkDownloadVersion |  | X | When specified, checks the latest Power BI Desktop version available from download.microsoft.com. |

    pbi-tools.exe info check

_Prints information about the active version of pbi-tools, all Power BI Desktop versions on the local system, any running Power BI Desktop instances, and checks the latest version of Power BI Desktop available from Microsoft Downloads._

#### cache

    cache <action> 

Manages the internal assembly cache.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| action* |  |  | The cache action to perform. <br> `List`  - List all cache folders. <br> `ClearAll`  - Clear all cache folders. <br> `ClearOutdated`  - Clear all cache folders except the most recent one. |

    pbi-tools.exe cache list

_Lists all cache folders present in the current user profile._

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
