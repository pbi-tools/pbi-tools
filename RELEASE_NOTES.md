
#### 0.1.0-beta1 - 2018-xx-xx
* Dynamic discovery of Power BI installations, including Windows Store installs (64-bit only)
* Shadow-copying of msmdsrv files when using AppModel binaries
* FAKE build script (Clean, Build implemented)
* Added 'info' cli action, listing all available PBI Desktop installs (to be extended with further items)
* Logging improved 
* WIP: Extraction of Version, ReportMetadata, Connections, LinguisticSchema, ReportSettings, DataModelSchema, MashupPackageMetadata
* WIP: ILMERGE/Bundling

#### 0.0.0-alpha1 - 2018-02-26
* First preview: Implements 'extract' action (Model, Mashup, Report) 
* Limitations: 64-Bit only, requires PBI Desktop install in default location, won't extract PBIT DataModelSchema
* Targeting net45
* Initial '.pbixproj.json' implementation (version: 0.0)
* CLI using PowerArgs
