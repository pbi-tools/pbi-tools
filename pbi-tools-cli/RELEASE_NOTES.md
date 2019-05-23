
#### 0.6.0 - 2019-03-15
* PBIXPROJ format 0.3.1
  * Tabular model measure 'extendedProperties' are now supported (extraction previously failed)
* Major rewrite of internal serialization infrastructure

#### 0.5.0 - 2018-11-02
* Rebranding to "PBI Tools" (pbi-tools.exe) to leave scope for more general Power BI tooling that's not directly tied to PBIX files only

#### 0.4.0 - 2018-10-30
* PBIXPROJ format 0.3
  * Mashup metadata now being extracted into folder structure rather than a single xml file
  * Mashup (package) formulas extracted into folder structure instead of single "Section1.m"
* Report extraction improvements: git diffs are now a lot less noisy as json documents are transformed to come out in a predictable format
  * Json properties are always sorted alphabetically, Numbers are converted from float to int where possible; 'queryHash' and 'objectId' properties removed
* CLI usage improved

#### 0.3.0 - 2018-05-22
* PBIXPROJ format 0.2: 'dataSources' renamed to 'queries' to be consistent with internal PowerBI APIs
* Bundling all dependencies into 'pbix-tools.exe' so that there is only one executable to distribute (using Costura.Fody)
* Change: Mashup extraction format changed (full mashup package in /Mashup/Package)
* Feature: Added extraction support for: Version, Connections, ReportMetadata, ReportSettings, LinguisticSchema, MashupPackageMetadata, MashupPackageContent
* Fix: Full cleanup of deleted files (between extractions)
* Breaking Change: target framework is now .Net 4.5.2 (required by Costura.Fody)

#### 0.2.0-beta.1 - 2018-04-17
* Handle PBIX w/o embedded model (live connection or PBIT)

#### 0.1.0-beta.3 - 2018-03-13
* [info] action returns 'effectivePowerBiFolder'

#### 0.1.0-beta.2 - 2018-03-12
* PBIXPROJ v0.1: Model/dataSources: use location (query name) as folder name (rather than datasource guid); always write 'dataSource.json'

#### 0.1.0-beta.1 - 2018-03-11
* Dynamic discovery of Power BI installations, including Windows Store installs (64-bit only)
* Shadow-copying of msmdsrv files when using AppModel binaries
* FAKE build script (Clean, Build implemented)
* Added 'info' cli action, listing all available PBI Desktop installs (to be extended with further items)
* Logging improved 

#### 0.0.0-alpha.1 - 2018-02-26
* First preview: Implements 'extract' action (Model, Mashup, Report) 
* Limitations: 64-Bit only, requires PBI Desktop install in default location, won't extract PBIT DataModelSchema
* Targeting net45
* Initial '.pbixproj.json' implementation (version: 0.0)
* CLI using PowerArgs
