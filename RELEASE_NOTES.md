
#### 0.1.0-beta1.1 - 2018-03-12
* PBIXPROJ v0.1: Model/dataSources: use location (query name) as folder name (rather than datasource guid); always write 'dataSource.json'
* WIP: Extraction of Version, ReportMetadata, Connections, LinguisticSchema, ReportSettings, DataModelSchema, MashupPackageMetadata
* WIP: ILMERGE/Bundling

#### 0.1.0-beta1 - 2018-03-11
* Dynamic discovery of Power BI installations, including Windows Store installs (64-bit only)
* Shadow-copying of msmdsrv files when using AppModel binaries
* FAKE build script (Clean, Build implemented)
* Added 'info' cli action, listing all available PBI Desktop installs (to be extended with further items)
* Logging improved 

#### 0.0.0-alpha1 - 2018-02-26
* First preview: Implements 'extract' action (Model, Mashup, Report) 
* Limitations: 64-Bit only, requires PBI Desktop install in default location, won't extract PBIT DataModelSchema
* Targeting net45
* Initial '.pbixproj.json' implementation (version: 0.0)
* CLI using PowerArgs
