## Usage

    pbi-tools.core <action> -options

_pbi-tools (Core), 1.0.0-rc.4 - https://pbi.tools/_

### Actions

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
| modelSerialization |  |  | The model serialization mode. <br> `Default`  - The default serialization format, effective if no option is specified. The default is TMDL. <br> `Raw`  - Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. No transformations are applied. <br> `Legacy`  - Serializes the tabular model into the default PbixProj folder structure and performs various transformations to optimize file contents for source control. <br> `Tmdl`  - Serializes the tabular model into TMDL format. |
| mashupSerialization |  |  | The mashup serialization mode. <br> `Default`  - Similar to 'Raw' mode, with the exception that QueryGroups are extracted into a separate file for readability. <br> `Raw`  - Serializes all Mashup parts with no transformations applied. <br> `Expanded`  - Serializes the Mashup metadata part into a Json document, and embedded M queries into separate files. This mode is not supported for compilation. |
| settingsFile |  |  | An external .pbixproj.json file containing serialization settings. Serialization modes specified as command-line arguments take precedence. |
| updateSettings | `False` | X | If set, updates the effective PbixProj settings file used for this conversion. |
| modelOnly | `False` | X | If set, converts the model only and leaves other artifacts untouched. Only effective in combination with a PbixProj source folder. |
| overwrite | `False` | X | Allows overwriting of existing files in the destination. The conversion fails if the destination is not empty and this flag is not set. |

#### deploy

    deploy <folder> <label> [<environment>] [<basePath>] [<whatIf>] 

Deploys artifacts to Power BI Service or Azure Analysis Services using a deployment manifest. Currently, only 'Report' deployment, from .pbix files or PbixProj folders, is supported.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder containing the deployment manifest. |
| label* |  |  | Name of a profile in the deployment manifest. |
| environment | `Development` |  | The target deployment environment. |
| basePath |  |  | When specified, resolves all deployment source paths relative to this path (and basePath relative to the current working directory), instead of the location of the PbixProj manifest. |
| whatIf | `False` | X | When enabled, simulates the deployment actions and provides diagnostic output. Useful to test source path expressions and parameters. Authentication credentials are validated. |

#### export-data

    export-data [<port>] [<outPath>] [<dateTimeFormat>] 

Exports data from all tables in a live Power BI Desktop session.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| port |  |  | The port number of a local Tabular Server instance. |
| outPath |  |  | The output directory. Uses PBIX file directory if not provided, or the current working directory when connecting to Tabular Server instance. |
| dateTimeFormat | `s` |  | The format to use for DateTime values. Must be a valid .Net format string, see: https://docs.microsoft.com/dotnet/standard/base-types/standard-date-and-time-format-strings. |

**Export data from local workspace instance**

    pbi-tools.core export-data -port 12345

_Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

#### generate-bim

    generate-bim <folder> [<transforms>] 

Generates a TMSL/BIM file from Model sources in a folder. The output path is derived from the source folder.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to export the BIM file from. |
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

