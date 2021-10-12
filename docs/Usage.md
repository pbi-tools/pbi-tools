## Usage

    pbi-tools <action> -options

_Action BI Toolkit | pbi-tools, 1.0.0-beta.6_

### Actions

#### extract

    extract <pbixPath> [<pbiPort>] [<extractFolder>] [<mode>] [<modelSerialization>] [<mashupSerialization>] 

Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| pbixPath* |  |  | The path to an existing PBIX file. |
| pbiPort |  |  | The port number from a running Power BI Desktop instance. When specified, the model will not be read from the PBIX file, and will instead be retrieved from the PBI instance. Only supported for V3 PBIX files. |
| extractFolder |  |  | The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory. |
| mode | `Auto` |  | The extraction mode. <br> `Auto`  - Attempts extraction using the V3 model, and falls back to Legacy mode in case the PBIX file does not have V3 format. <br> `V3`  - Extracts V3 PBIX files only. Fails if the file provided has a legacy format. <br> `Legacy`  - Extracts legacy PBIX files only. Fails if the file provided has the V3 format. |
| modelSerialization |  |  | The model serialization mode. <br> `Default`  - Serializes the tabular model into a standard folder structure and performs various transformations to optimize file contents for source control. <br> `Raw`  - Serializes the tabular model into a single JSON file containing the full TMSL payload from the PBIX model. |
| mashupSerialization |  |  | The mashup serialization mode. <br> `Default`  - Similar to 'Raw' mode, with the exception that QueryGroups are extracted into a separate file for readability. <br> `Raw`  - Serializes all Mashup parts with no transformations applied. <br> `Expanded`  - Serializes the Mashup metadata part into a Json document, and embedded M queries into separate files. This mode is not supported for compilation. |

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
| outPath |  |  | The output directory. Uses PBIX file directory if not provided, or the current working directory when connecting to Tabular Server instance. |
| dateTimeFormat | `s` |  | The format to use for DateTime values. Must be a valid .Net format string. |

**Extract data from local workspace instance**

    pbi-tools.exe extract-data -port 12345

_Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

**Extract data from offline PBIX file**

    pbi-tools.exe extract-data -pbixPath '.\data\Samples\Adventure Works DW 2020.pbix'

_Extracts all records from each table from the model embedded in the specified PBIX file. Each table is extracted into a UTF-8 CSV file with the same name into the current working directory._

#### export-bim

    export-bim <folder> [<generateDataSources>] [<transforms>] 

Converts the Model artifacts to a TMSL/BIM file.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to export the BIM file from. |
| generateDataSources |  | X | Generate model data sources. Only required for deployment to Azure Analysis Services, but not for Power BI Premium via the XMLA endpoint. |
| transforms |  |  | List transformations to be applied to TMSL document. <br> `RemovePBIDataSourceVersion`  - Removes the 'defaultPowerBIDataSourceVersion' model property, making the exported BIM file compatible with Azure Analysis Services. |

#### compile-pbix

    compile-pbix <folder> [<outPath>] [<format>] [<overwrite>] 

*EXPERIMENTAL* Generates a PBIX/PBIT file from sources in the specified PbixProj folder. Currently, the PBIX output is supported only for report-only projects, and PBIT for projects containing a data model.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to generate the PBIX from. |
| outPath |  |  | The path for the output file. If not provided, creates the file in the current working directory, using the foldername. A directory or file name can be provided. The full output path is created if it does not exist. |
| format | `PBIX` |  | The target file format. <br> `PBIX`  - Creates a file using the PBIX format. If the file contains a data model it will have no data and will require processing. This is the default format. <br> `PBIT`  - Creates a file using the PBIT format. When opened in Power BI Desktop, parameters and/or credentials need to be provided and a refresh is triggered. |
| overwrite |  | X | Overwrite the destination file if it already exists, fail otherwise. |

#### launch-pbi

    launch-pbi <pbixPath> 

Starts a new instance of Power BI Desktop with the PBIX/PBIT file specified. Does not support Windows Store installations.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| pbixPath* |  |  | The path to an existing PBIX or PBIT file. |

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

