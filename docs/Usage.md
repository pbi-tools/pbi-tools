## Usage

    pbi-tools <action> -options

_pbi-tools-cli, 1.0.0-beta.3_

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

#### compile-pbix

    compile-pbix <folder> [<pbixPath>] [<format>] 

*EXPERIMENTAL* Generates a PBIX/PBIT file from sources in the specified PbixProj folder.

| Option | Default Value | Is Switch | Description |
| --- | --- | --- | --- |
| folder* |  |  | The PbixProj folder to generate the PBIX from. |
| pbixPath |  |  | The path for the output file. If not provided, creates the file in the current working directory, using the foldername. |
| format | `Pbix` |  |  <br> `Pbix`  - Creates a file using the PBIX format. If a data model is loaded into the file it will have no data and will require processing. This is the default format. <br> `Pbit`  - Creates a file using the PBIT format. When opened in Power BI Desktop, parameters/credentials need to be provided and a refresh is triggered. |

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

