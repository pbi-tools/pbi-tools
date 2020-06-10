
## 1.0.0-alpha.2 - 2020-06-10
* Changed target framework to .Net 4.7.2 (allows compatibility with external libraries that only support .Net Standard 2.0, rather than legacy .Net Framework versions)
* Upgraded AMO library to 19.2.0.2, supporting latest TMSL features
* Upgraded various other 3rd party dependencies
* Support for new PBIX metadata format ("V3"), introduced in March 2020 version of Power BI Desktop
  * `PbixModel` API now available
* PBIXPROJ format 0.5
* New CLI action: "export-bim"
* "info" action: Added 'version', 'pbiSessions'
* "export-bim" action added
* Compatible with Power BI Desktop May 2020 version
* Significantly reduced exe file size by excluding 3rd party satellite assemblies
* Support for 'PBITOOLS_PbiInstallDir', 'PBITOOLS_LogLevel' environment variables
* Application icon added
* V3 files are supported with LinguisticSchema in either xml (legacy) or json format

## 0.10.0 - 2019-11-14
* Upgraded AMO library to 18.2
* Addresses breaking API change in Nov 2019 release (2.75) of Power BI Desktop. Modification is backwards-compatible, however, so will still work with the Oct release.
  * Details: `ReportMetadata` now has a dependency on <IFeatureSwitchManager> (also added DirectQueryResources, QueryDependencyGraph - for v3 report model)
* Fixed duplicated "DiagramViewState extracted" message

## 0.9.0 - 2019-10-20
* Upgraded AMO library to 18.0 so that latest tabular features are recognized by serializer (Specifically, measure/dataCategory)
* Addresses breaking API change in Oct 2019 release (2.74) of Power BI Desktop
  * Pre 2.74, IBinarySerializable was defined in Microsoft.Mashup.Client.Packaging.dll (implemented by ReportSettings, ReportMetadata, QueryGroupMetadata)
  * Since 2.74, that interface has been moved to Microsoft.PowerBI.Packaging.dll
* [info] action: Added 'amoVersion' property, returning the product version of the AMO library in use

## 0.8.1 - 2019-06-24
* PBIXPROJ format 0.4.1
  * FIX: Url-encode measure names when serializing to file system to allow for illegal path characters

## 0.8.0 - 2019-05-24
* PBIXPROJ format 0.4
  * Excluding Report/section/id (field is volatile and 'name' is already a unique identifier for sections)

## 0.7.0 - 2019-05-23
* Addresses breaking API change in May 2019 release of Power BI Desktop (i.e., this version is incompatible with earlier versions)

## 0.6.0 - 2019-03-15
* PBIXPROJ format 0.3.1
  * Tabular model measure 'extendedProperties' are now supported (extraction previously failed)
* Major rewrite of internal serialization infrastructure

## 0.5.0 - 2018-11-02
* Rebranding to "PBI Tools" (pbi-tools.exe) to leave scope for more general Power BI tooling that's not directly tied to PBIX files only

## 0.4.0 - 2018-10-30
* PBIXPROJ format 0.3
  * Mashup metadata now being extracted into folder structure rather than a single xml file
  * Mashup (package) formulas extracted into folder structure instead of single "Section1.m"
* Report extraction improvements: git diffs are now a lot less noisy as json documents are transformed to come out in a predictable format
  * Json properties are always sorted alphabetically
  * Numbers are converted from float to int where possible
  * 'queryHash' and 'objectId' properties removed
* CLI usage improved

## 0.3.0 - 2018-05-22
* PBIXPROJ format 0.2: 'dataSources' renamed to 'queries' to be consistent with internal PowerBI APIs
* Bundling all dependencies into 'pbix-tools.exe' so that there is only one executable to distribute (using Costura.Fody)
* Change: Mashup extraction format changed (full mashup package in /Mashup/Package)
* Feature: Added extraction support for: Version, Connections, ReportMetadata, ReportSettings, LinguisticSchema, MashupPackageMetadata, MashupPackageContent
* Fix: Full cleanup of deleted files (between extractions)
* Breaking Change: target framework is now .Net 4.5.2 (required by Costura.Fody)

## 0.2.0-beta.1 - 2018-04-17
* Handle PBIX w/o embedded model (live connection or PBIT)

## 0.1.0-beta.3 - 2018-03-13
* [info] action returns 'effectivePowerBiFolder'

## 0.1.0-beta.2 - 2018-03-12
* PBIXPROJ v0.1: Model/dataSources: use location (query name) as folder name (rather than datasource guid); always write 'dataSource.json'

## 0.1.0-beta.1 - 2018-03-11
* Dynamic discovery of Power BI installations, including Windows Store installs (64-bit only)
* Shadow-copying of msmdsrv files when using AppModel binaries
* FAKE build script (Clean, Build implemented)
* Added 'info' cli action, listing all available PBI Desktop installs (to be extended with further items)
* Logging improved 

## 0.0.0-alpha.1 - 2018-02-26
* First preview: Implements 'extract' action (Model, Mashup, Report) 
* Limitations: 64-Bit only, requires PBI Desktop install in default location, won't extract PBIT DataModelSchema
* Targeting net45
* Initial '.pbixproj.json' implementation (version: 0.0)
* CLI using PowerArgs
