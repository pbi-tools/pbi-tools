## Usage

    pbi-tools <action> -options

_pbi-tools (Desktop), 1.1.0 - https://pbi.tools/_

### Actions

#### cache

    cache <action> 

Manages the internal assembly cache.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| action* |  |  | The cache action to perform. <br> `List`  - List all cache folders. <br> `ClearAll`  - Clear all cache folders. <br> `ClearOutdated`  - Clear all cache folders except the most recent one. |

    pbi-tools cache list

_Lists all cache folders present in the current user profile._

#### compile

    compile <folder> [<outPath>] [<format>] [<overwrite>] 

Generates a PBIX/PBIT file from sources in the specified PbixProj folder. Currently, the PBIX output is supported only for report-only projects ("thin" reports), and PBIT for projects containing a data model.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to generate the PBIX from. |
| outPath |  |  | The path for the output file. If not provided, creates the file in the current working directory, using the foldername. A directory or file name can be provided. The full output path is created if it does not exist. |
| format | `PBIX` |  | The target file format. <br> `PBIX`  - Creates a file using the PBIX format. Only supported for "thin" reports - use the PBIT format if the project contains a data model. This is the default format. <br> `PBIT`  - Creates a file using the PBIT format. Use for data models. When opened in Power BI Desktop, parameters and/or credentials need to be provided and a refresh is triggered. |
| overwrite | `False` | X | Overwrite the destination file if it already exists, fail otherwise. |

#### convert

    convert <source> [<outPath>] [<modelSerialization>] [<mashupSerialization>] [<settingsFile>] [<updateSettings>] [<modelOnly>] [<overwrite>] 

Performs an offline conversion of PbixProj or Tabular model sources into another format, either in-place or into another destination.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| source* |  |  | The source(s) to convert. Can be a PbixProj folder, a Model/TE folder, or a TMSL json file. |
| outPath |  |  | The (optional) destination. Can be a folder or a file, depending on the conversion mode. Must be a folder if the source is a TMSL json file. |
| modelSerialization |  |  | The model serialization mode. <br> `Default`  - The default serialization format, effective if no option is specified. The default is TMDL. <br> `Raw`  - Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. No transformations are applied. <br> `Legacy`  - Serializes the tabular model into the default PbixProj folder structure and performs various transformations to optimize file contents for source control. <br> `Tmdl`  - Serializes the tabular model into TMDL format. Annotation settings are applied. |
| mashupSerialization |  |  | The mashup serialization mode. <br> `Default`  - Similar to 'Raw' mode, with the exception that QueryGroups are extracted into a separate file for readability. <br> `Raw`  - Serializes all Mashup parts with no transformations applied. <br> `Expanded`  - Serializes the Mashup metadata part into a Json document, and embedded M queries into separate files. This mode is not supported for compilation. |
| settingsFile |  |  | An external .pbixproj.json file containing serialization settings. Serialization modes specified as command-line arguments take precedence. |
| updateSettings | `False` | X | If set, updates the effective PbixProj settings file used for this conversion. |
| modelOnly | `False` | X | If set, converts the model only and leaves other artifacts untouched. Only effective in combination with a PbixProj source folder. |
| overwrite | `False` | X | Allows overwriting of existing files in the destination. The conversion fails if the destination is not empty and this flag is not set. |

#### deploy

    deploy <folder> <label> [<environment>] [<basePath>] [<whatIf>] 

Deploys artifacts (reports, datasets) to Power BI Service using a deployment manifest.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder containing the deployment manifest. |
| label* |  |  | Name of a profile in the deployment manifest. |
| environment | `Development` |  | The target deployment environment. |
| basePath |  |  | When specified, resolves all deployment source paths relative to this path (and basePath relative to the current working directory), instead of the location of the PbixProj manifest. |
| whatIf | `False` | X | When enabled, simulates the deployment actions and provides diagnostic output. Useful to test source path expressions and parameters. Authentication credentials are validated. |

#### export-data

    export-data [<port>] <pbixPath> [<outPath>] [<dateTimeFormat>] 

Exports data from all tables in a tabular model, either from within a PBIX file, or from a live session.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| port |  |  | The port number of a local Tabular Server instance. |
| pbixPath* |  |  | The PBIX file to extract data from. |
| outPath |  |  | The output directory. Uses PBIX file directory if not provided, or the current working directory when connecting to Tabular Server instance. |
| dateTimeFormat | `s` |  | The format to use for DateTime values. Must be a valid .Net format string, see: https://docs.microsoft.com/dotnet/standard/base-types/standard-date-and-time-format-strings. |

**Export data from local workspace instance**

    pbi-tools export-data -port 12345

_Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

**Export data from offline PBIX file**

    pbi-tools export-data -pbixPath '.\data\Samples\Adventure Works DW 2020.pbix'

_Extracts all records from each table from the model embedded in the specified PBIX file. Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

#### extract

    extract <pbixPath> <pid> [<pbiPort>] [<extractFolder>] [<mode>] [<modelSerialization>] [<mashupSerialization>] [<watch>] 

Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| pbixPath* |  |  | The path to an existing PBIX file. |
| pid* |  |  | The Power BI Desktop process ID to extract sources from (look up via 'pbi-tools info'). |
| pbiPort |  |  | The port number from a running Power BI Desktop instance (look up via 'pbi-tools info'). When specified, the model will not be read from the PBIX file, and will instead be retrieved from the PBI instance. Only supported for V3 PBIX files. |
| extractFolder |  |  | The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory. |
| mode | `Auto` |  | The extraction mode. <br> `Auto`  - Attempts extraction using the V3 model, and falls back to Legacy mode in case the PBIX file does not have V3 format. <br> `V3`  - Extracts V3 PBIX files only. Fails if the file provided has a legacy format. <br> `Legacy`  - Extracts legacy PBIX files only. Fails if the file provided has the V3 format. |
| modelSerialization |  |  | The model serialization mode. <br> `Default`  - The default serialization format, effective if no option is specified. The default is TMDL. <br> `Raw`  - Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. No transformations are applied. <br> `Legacy`  - Serializes the tabular model into the default PbixProj folder structure and performs various transformations to optimize file contents for source control. <br> `Tmdl`  - Serializes the tabular model into TMDL format. Annotation settings are applied. |
| mashupSerialization |  |  | The mashup serialization mode. <br> `Default`  - Similar to 'Raw' mode, with the exception that QueryGroups are extracted into a separate file for readability. <br> `Raw`  - Serializes all Mashup parts with no transformations applied. <br> `Expanded`  - Serializes the Mashup metadata part into a Json document, and embedded M queries into separate files. This mode is not supported for compilation. |
| watch |  | X | Enables watch mode. Monitors the PBIX file open in a Power BI Desktop session, and extracts sources each time the file is saved. |

**Extract: Custom folder and settings**

    pbi-tools extract '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw

_Extracts the PBIX file into the specified extraction folder (relative to the current working dir), using the 'Auto' compatibility mode. The model part is serialialized using Raw mode._

**Extract: Default**

    pbi-tools extract '.\data\Samples\Adventure Works DW 2020.pbix'

_Extracts the specified PBIX file into the default extraction folder (relative to the PBIX file location), using the 'Auto' compatibility mode. Any settings specified in the '.pbixproj.json' file already present in the destination folder will be honored._

#### extract-pbidesktop

    extract-pbidesktop <installerPath> [<targetFolder>] [<overwrite>] 

Extracts binaries from a PBIDesktopSetup.exe|.msi installer bundle (silent/x-copy install).

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| installerPath* |  |  | The path to an existing PBIDesktopSetup.exe|PBIDesktopSetup.msi file. |
| targetFolder |  |  | The destination folder. '-overwrite' must be specified if folder is not empty. |
| overwrite | `False` | X | Overwrite any contents in the destination folder. Default: false |

#### generate-bim

    generate-bim <folder> [<generateDataSources>] [<transforms>] 

Generates a TMSL/BIM file from Model sources in a folder. The output path is derived from the source folder.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to export the BIM file from. |
| generateDataSources |  | X | Generate model data sources. Only required for deployment to Azure Analysis Services, but not for Power BI Premium via the XMLA endpoint. |
| transforms |  |  | List transformations to be applied to TMSL document. <br> `RemovePBIDataSourceVersion`  - Removes the 'defaultPowerBIDataSourceVersion' model property, making the exported BIM file compatible with Azure Analysis Services. |

#### git

    git <action> 

Integrates with git and exposes certain actions to perform in the current working directory.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| action* |  |  | The git action to perform. <br> `Branch`  - Displays the active git branch in the current working directory. |

    pbi-tools git branch

_Displays the active git branch in the current working directory. Automatically detects the root of the current git repository._

#### info

    info [<checkDownloadVersion>] 

Collects diagnostic information about the local system and writes a JSON object to StdOut.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| checkDownloadVersion |  | X | When specified, checks the latest Power BI Desktop version available from download.microsoft.com. |

    pbi-tools info check

_Prints information about the active version of pbi-tools, all Power BI Desktop versions on the local system, any running Power BI Desktop instances, and checks the latest version of Power BI Desktop available from Microsoft Downloads._

#### init

    init <action> [<folder>] 

Initializes a PbixProj workpace.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| action* |  |  | The initialize action to perform. <br> `Deployments`  - Generates a sample deployment manifest in the specified location if none exists. |
| folder |  |  | The PbixProj folder to operation in. Uses current working directory if not specified. |

#### launch-pbi

    launch-pbi [<pbixPath>] 

Starts a new instance of Power BI Desktop, optionally loading a specified PBIX/PBIT file. Does not support Windows Store installations.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| pbixPath |  |  | The path to an existing PBIX or PBIT file. |

