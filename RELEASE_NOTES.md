# Release Notes

## 1.2.0 - 2024-01-06

- Upgraded project to .Net SDK 9.0 (GA 12-Nov)
- Switched net6.0 build to net9.0 - "NetCore" remains on LTS version, net8.0
  - New distribution "pbi-tools.net9.\*.zip" instead of "pbi-tools.net6.\*.zip"
- Dependencies updated:
  - TOM, 19.87.7 (Jan-2025 Version)
  - MSAL.NET, 4.66.2
  - Power BI API, 4.22
  - Polly, 8.5
  - Serilog, 4.2
  - Fody/Costura, 6.0 - #365
  - System.*, 9.0
- #359 Resolve XMLA endpoint when workspace Guid is provided
- Various GitHub CI workflow and build script enhancements
- #351 Workaround reverted

## 1.1.1 - 2024-10-14

- Resolves [#351](https://github.com/pbi-tools/pbi-tools/issues/351)
  - Implements workaround for linux-specific bug in TOM
- Adds CI integration test performing an e2e semantic model deployment (TMDL)

## 1.1.0 - 2024-10-09

- Dependencies updated:
  - TOM, 19.84.6 (TMDL GA)
  - MSAL.NET, 4.65
- Set up CI build on GitHub Actions (excludes net472 targets due to PBI Desktop dependency)

## 1.0.1 - 2024-07-23

- Fixed [#343](https://github.com/pbi-tools/pbi-tools/issues/343) Report/DisplayName set incorrectly when wildcard source is used
- Resolved [#324](https://github.com/pbi-tools/pbi-tools/issues/324) Add DESCRIPTION to DOCKERFILE
- Resolved [#341](https://github.com/pbi-tools/pbi-tools/issues/341) Upgraded GH Actions workflow to latest
- Various internal improvements; Addressed all compiler warnings
- LICENSE: All C# file headers updated
- Dependencies updated:
  - System.IdentityModel.*, 8.0
  - dbup-sqlserver, 5.0.41

## 1.0.0 - 2024-07-16

- Released under the AGPL-3.0 license
- Upgraded to TOM 19.84.1 (TMDL Preview-15) - aligns with Tabular Editor 2.25
- Gateway binding: New _Always_ mode introduced
- In Dataset `WhatIf` mode, the target _capacity_ (if dedicated) is reported alongside the target workspace
- Fixed [#338](https://github.com/pbi-tools/pbi-tools/issues/338) Deployment logging bug with unescaped markup
- Implemented [#331](https://github.com/pbi-tools/pbi-tools/issues/331) Should report data sources **after** binding to gateway
- [#339](https://github.com/pbi-tools/pbi-tools/issues/339) Updated Core version to .Net 8.0; dropped support for .Net 7.0
- [#339](https://github.com/pbi-tools/pbi-tools/issues/339) Updated Docker version to .Net 8.0
- [#43](https://github.com/pbi-tools/pbi-tools/issues/43) All Windows executables (Desktop & Core) are now digitally signed
- Numerous dependencies updated
- PbixProj v1.0 Schema
  - `settings.model.metadataOrderHints`
  - `settings.model.expressionTrimStyle` _(Note this is an EnumFlags property which is serialized as comma-separated strings)_
  - `manifest.options.dataset.gateway.mode` { OnCreation, Disabled, Always }

## 1.0.0-rc.8 - 2024-01-09

- Upgraded to TOM 19.74.2 (TMDL Preview-9)
- PbixProj v0.14 Schema (_Note that the RC.3 release should have had a version bump, too._)
  - settings.model.excludeChildrenMetadata
  - settings.model.includeRestrictedInformation
  - settings.model.formatting.encoding
  - settings.model.formatting.newLineStyle
  - settings.model.formatting.indentationMode
  - settings.model.formatting.indentationSize
- Various dependencies updated

## 1.0.0-rc.7 - 2023-11-28

- Upgraded to TOM 19.69.6.2 (TMDL Preview-7; aligns with Tabular Editor 2.21.x)

## 1.0.0-rc.5 - 2023-11-28

- Upgraded to TOM 19.67 (TMDL Preview-5; aligns with Tabular Editor 2.20.x)
- Temp fix for #290 (Cannot deploy/compile TMDL model with Power BI-specific artifacts, for instance field params)
  - To be reverted once the next TMDL preview handles this scenario internally

## 1.0.0-rc.4 - 2023-05-09

- Upgraded to TOM 19.64, including **TMDL Preview-2**
  - Fixes #265, #273
- #232 CI Runner Console Output
  - `manifest.options.console.width`: _If specified, sets an explicit console width. This setting can be useful with certain CI/CD runners._
  - `manifest.options.console.expandTable`: _Indicates whether or not tables printed to the console should fit the available space. If `false` (default), the table width will be auto calculated._
- #274 Allow running pbi-tools with private AMO DLLs
  - New env setting: `PBITOOLS_ExternalAmoPath` (Desktop/NetFx only)
  - If provided and a valid directory, loads all _"Microsoft.AnalysisServices*.dll"_ assemblies from that folder into current AppDomain (prior to Fody embedded dlls).
  - Allows users to run pbi-tools session with private set of AMO DLLs
- #272 Simplified error reporting (no stack trace) for certain known exception types
  - IOException: _The process cannot access the file because it is being used by another process._
- Fixed #269 Typo in deployment error message
- PbixProj v0.13 Schema (_Note that the RC.3 release should have had a version bump, too._)
  - #195 OAuth2 creds in deployment manifest { scopes, useDeploymentToken }
  - #232 Deployment log setting: manifest.options.console { width, expandTable }

## 1.0.0-rc.3 - 2023-04-11

- #262 TMDL Serialization Support
  - New model serialization modes: `Tmdl` (default), `Legacy` (PbixProj)
  - New (optional) environment variable: `PBITOOLS_DefaultModelSerialization`
- New CLI action: extract-pbidesktop
  - Extracts binaries from a PBIDesktopSetup.exe|.msi installer bundle (silent/x-copy install). (Implemented using an embedded tool: wix-extract.exe)
  - Arguments:
    - `<installerPath>` - The path to an existing PBIDesktopSetup.exe|PBIDesktopSetup.msi file.
    - `<targetFolder>` - The destination folder. '-overwrite' must be specified if folder is not empty.
    - `<overwrite>` - Overwrite any contents in the destination folder. Default: false
- Change to 'launch-pbi' CLI action: `<pbixPath>` argument is now optional. If not specified, a new PBIDesktop instance is started without opening an existing file.
- New build target: "BuildTools" - Builds all csproj inside ./tools and bundles each tool output as a .zip archive in ./.build/out/*.zip

- #195 Set (Cloud) credentials during dataset deployment - Anonymous
- #195 Set (Cloud) credentials during dataset deployment - OAuth2
- #195 Case-insensitive matching of data sources
- Manifest Schema Changes 0.13
  - manifest.credentials[].updateMode: { NotSpecified, Always, Never, BeforeRefresh }
  - manifest.credentials[].type: { Basic, Anonymous, OAuth2 }
  - manifest.credentials[].authority
  - manifest.credentials[].validateAuthority
  - manifest.credentials[].tenantId
  - manifest.credentials[].clientId
  - manifest.credentials[].clientSecret
  - manifest.credentials[].scopes
  - manifest.credentials[].useDeploymentToken

**Example: OAuth2 credentials for another PBI dataset**

```json
      "credentials": [
        {
          "match": {
            "datasourceType": "AnalysisServices",
            "connectionDetails": {
              "server": "powerbi://api.powerbi.com/v1.0/myorg/WORKSPACE",
              "database": "DATASET_NAME"
            }
          },
          "type": "OAuth2",
          "updateMode": "BeforeRefresh",
          "tenantId": "your-tenant.com",
          "clientId": "c2e24fe7-4785-4776-9040-eb19c16ed700",
          "clientSecret": "%CLIENT_SECRET%",
          "scopes": [
            "https://analysis.windows.net/powerbi/api/.default"
          ]
        }
      ]
```

**Example: Use deployment token to access another PBI dataset during refresh**

_This will only work for other Power BI resources (because of OAuth scope). Ensure that the service principal used for the deployment has sufficient access to the referenced dataset._

```json
      "credentials": [
        {
          "match": {
            "datasourceType": "AnalysisServices",
            "connectionDetails": {
              "server": "powerbi://api.powerbi.com/v1.0/myorg/WORKSPACE",
              "database": "DATASET_NAME"
            }
          },
          "type": "OAuth2",
          "updateMode": "BeforeRefresh",
          "useDeploymentToken": true
        }
      ]
```

## 1.0.0-rc.2 - 2023-01-09

- **#97 Model Deployments**
- #147 Refresh Tracing
- #141 Deployment of "thick" reports
- #145 Non-string deployment parameters
- #146 Environment-scoped parameters
- #168 SqlScripts Deployments
- #129 Object-specific refresh
- #135 Bind to Gateway (new datasets)
- #167 Report partition status after update/refresh
- #169 Report datasources
- #151 Deployments of Incremental Refresh datasets
- #195 Set (Cloud) credentials during dataset deployment - Basic
- **#26 Bookmarks** (PbixProj v0.12 schema)
- #91 Serialize/Deserialize _MobileState_
- #153 Make "CreateOrOverwrite" default import mode
- #202 Ship .Net 7 version of pbi-tools Core
- #56 Support for long paths on Windows
- Fixed #109 'pbi-tools info' no longer fails when another instance of SSAS runs on the same machine
- Fixed #127 Folder or File sources containing spaces aren't matched (Desktop edition only)
- Fixed #102 x-plat conform resolution of TEMP path
- Fixed #111 Deployment fails in model-only mode (due to logging)
- Fixed #207 Dataset deployment fails if model has field parameters
- Fixed #219 pbi-tools Core does not compress PBIX files when compiling
- Libraries updated: TOM 19.54, Power BI API 4.11, MSAL 4.49, db-up 5.0
- Tested with PBI Desktop 2.112 (Dec '22)
- Converted Fake build system from runner to fsproj

**System params expansion (#157)**

- Explicit manifest parameters can now reference system parameters, including `{{ENVIRONMENT}}`, `{{PBITOOLS_VERSION}}`, `{{FILE_NAME}}`, `{{FILE_NAME_WITHOUT_EXT}}`, `{{PBIXPROJ_FOLDER}}`

Example:
```json
  "parameters": {
    "[Version]": "1.1.0",
    "[Environment]": "{{ENVIRONMENT}}",
    "[PBITOOLS_VERSION]": "{{PBITOOLS_VERSION}}",
    "[GH-Branch]": "%GITHUB_REF_NAME%",
    "[GH-RunId]": "%GITHUB_RUN_ID%",
    "[GH-SHA]": "%GITHUB_SHA%",
    "[FilterDate]": null
  }
```

**Refresh Tracing and Refresh Summary Stats (#147)**

- Retrieves traces during XMLA refresh, emits live logs and generates a refresh summary
- Tracing can be entirely disabled
- If enabled, events can be logged to the console. Any number of filter expressions are allowed. Filters are evaluated against composite keys: "{EventClass}|{EventSubclass}|{ObjectType}". Wildcards ('*', '?') are allowed.
- Furthermore, a refresh summary can be produced, either as a console output and/or into a UTF-8 csv file. Summary stats allow filtering by event and object type.
- All configuration for refresh tracing is held in options/refresh/tracing
- Possible values for EventSubclass (referenced in logEvents/filter and summary/events) are listed here: <https://docs.microsoft.com/en-us/analysis-services/trace-events/progress-reports-data-columns?view=sql-analysis-services-2022>

Example config section:
```json
"options": {
  "refresh": {
    "method": "XMLA",
    "type": "Full",
    "tracing": {
      "enabled": true,
      "logEvents": {
        "filter": [
          "*|TabularRefresh|Partition",
          "*|ReadData|Partition"
        ]
      },
      "summary": {
        "events": [
          "TabularRefresh",
          "Process",
          "ReadData",
          "ExecuteSql"
        ],
        "objectTypes": [
          "Partition"
        ],
        "outPath": "refresh_stats.csv",
        "console": true
      }
    }
  }
```

**Deploy embedded report alongside dataset (#141)**

- Allows deploying report from a PbixProj folder alongside dataset from same project
- Report is automatically bound to dataset. This happens offline, ensuring the PBIX has a valid dataset reference
- Custom connections.json template can be specified via manifest/options/report/customConnectionsTemplate (HTTP url or relative file path). The template must contain the "{{DATASET_ID}}" placeholder and must be valid json.
- Initial support for 'pbiServiceLive' connections only

Enabled as part of manifest/options/dataset:
```json
  "options": {
    "dataset": {
      "deployEmbeddedReport": true
    }
  }
```

Report name and destination are derived from dataset, but can be customized via environment settings. Paramter replacement supported for 'workspace' and 'displayName':
```json
  "UAT": {
    "workspace": "Datasets [PROD]",
    "displayName": "{{PBIXPROJ_FOLDER}} [UAT]",
    "refresh": true,
    "report": {
      "skip": false,
      "workspace": "Name-or-ID",
      "displayName": "Report Name.pbix"
    }
  }
```

Those changes provide the foundation for #66

**Non-string deployment parameters (#145)**

- Support text, number, bool, null, expression parameters
- Fixed '"' escaping in text params
- Enabled C# 'preview' LangVersion in test projects to use C#11 raw string literals for embedded json code

Example payload:
```json
{
    "Number": 1,
    "Number2": 0.4,
    "Null": null,
    "String": "foo",
    "Bool": true,
    "Date": "#date(2022, 6, 1)",
    "Duration": "#duration(5, 0, 0, 0)"
}
```

## 1.0.0-rc.1 - 2022-03-06

- PbixProj v0.11 Schema
  - #96 New Model settings: settings/model/measures (format, extractExpression)
  - #96 BREAKING CHANGE: Measures json format now default
  - #90 Always serialize (partial) partitions payload, ensuring 'queryGroup' property is retained
  - #19 Do not serialize empty model/annotations[]
  - #85 Visuals with titles only differing in casing are now extracted into unique folders
  - #91 Support for /Report/mobileState (mobileState.json, explorationState.json)
- #89 EXTRACT/WATCH Mode
  - Enabled using `-watch` flag
  - Requires `-pid {ProcessID}` argument specifying PID of PBI Desktop process to attach to
  - Example usage: `pbi-tools extract -pid 12345 -watch`
  - PBIX file path and model port number are derived from PBI session info (available via `pbi-tools info`)
  - Watch mode terminates when the PBI Desktop instance exits or on CTRL+C
- Fixed #72 `pbi-tools git branch` no longer propagates exception if no repo is found
- Fixed #79 Ensure `./.temp` exists on fresh clone of repo
- Fixed #78 `pbi-tools extract` fails when msmdsrv cannot be started
  - New env setting `PBITOOLS_Debug` will launch msmdsrv in debug mode (set to "1", "True", "true" to enable)
    - CWD is used as working directory (be careful as this creates a potentially large number of files!)
    - Working directory is not removed after extraction - files are kept for inspection
  - `<Language/>` setting in `msmdsrv.ini` is always left as "0" (instead of `CultureInfo.CurrentCulture.LCID`, which might lead to unsupported values on non-English OS)
  - Thanks to @janmechtel for diagnosing the issue!
- #81 Clarified Compile/Format requirements
  - Improved CLI docs
  - Always fail 'compile' when data model is detected and 'PBIX' was selected. Display error w/o stack trace for better readability.
- Fixed #85 Report visuals don't re-compile correctly when names differ only in casing ("READY" vs "Ready")
  - Now using case-insensitive folder name comparer
  - Thanks to @joeg76 for diagnosing and reporting!
- #90 Ensure "queryGroup" is always extracted for all partitions
  - Breaking Change: PBID M partition declarations now remain in table.json (ensuring additional properties like 'queryGroup' ar retained)
  - Thanks to @joeg76 for detailed issue report
- #96 New Measure Serialization Settings
  - ***BREAKING CHANGE***: 'json' now default, 'xml' opt-in
  - Fixes #87 and #93: Non-scalar measure properties are serialized and deserialized correctly
  - Thanks to @didierterrien and @scottstauffer-fc for reporting those!
- `pbi-tools info` Enhancements
  - "pbixProjVersion", "locale" added
- New ENV setting: 'PBITOOLS_UICulture'
  - Overwrite default UICulture set by OS
  - Use and name supported by <https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.createspecificculture>
  - Specify "IVL" for *Invariant Culture* (Code: 127)
- Docker image released for `pbi-tools Core`: <https://github.com/pbi-tools/pbi-tools/pkgs/container/pbi-tools-core>
- Build system: Upgraded FAKE to 5.22, Paket to 7.0.2
- Dependencies updated: PeNet (2.9.7), Power BI API (4.3), AMO/TOM (19.36), MSAL (4.42), HtmlAgilityPack (1.11.42)
- Tested with Power BI Desktop 2.102 (Feb 2022)

## 1.0.0-beta.8 - 2022-01-26

- #48 'deploy' action, 2nd release
  - Folder wildcard source
  - Source path parameters
  - Workspace Name & Id support
  - 'File' source
  - 'WhatIf' mode
  - Support for all Azure Clouds (options/pbiBaseUri, authentication/authority)
- #16 Core version
  - Breaking: Now targets .Net 6 (LTS release)
  - Removed various CLI options not applicable to Core edition (`cache`, `export-data`: *pbixPath*, `generate-bim`: *generateDataSources*)
  - Edition ("Desktop" vs "Core") now displayed in `info` output and usage docs
  - Dedicated usage page for Core version at <https://pbi.tools/cli/usage-core.html>
  - Increased test coverage
- PBIXPROJ v0.10 schema
  - #29 Support for custom settings in `.pbixproj.json` for integration with external tools
  - #48 Breaking: 'nameConflict' moved into deployments/options/import
  - #48 Breaking: 'workspaceId' is now 'workspace' in deployments/environment
  - #48 New: Optional 'description' in deployment profile
  - #19 New Model settings: settings/model/annotations (exclude, include)
- #61 New action: `convert`
- #19 New Model serialization rules
  - Model annotation exclude/include
  - Suggested setting: `{ "exclude": [ "PBI_*" ], "include": [ "PBI_QueryOrder" ]}`
- #59 Fixed: Measure/ExtendedProperties deserialization fails
- #44 BREAKING: Two CLI actions renamed
  - `extract-data` -> `export-data`
  - `export-bim` -> `generate-bim`
  - _Previous action names are still functional, but not exposed in usage docs._
- #60 Fixed: Introduced new env setting `PBITOOLS_AppDataDir`, allowing to customize the default AppData location (in the `%LOCALAPPDATA%` folder). When the Windows Store version of PBI Desktop is used, that location is used to shadow-copy the msmdsrv engine. Some organizations prevent running executables from within `%LOCALAPPDATA%`. This is only needed to extract from .pbix files with an embedded model.
- #64 New action: `init`
- #62 New action: `git`
- `info` action: *pbiInstallation* json no longer contains the `v3ModelEnabled` property (the V3 format is the default in PBI Desktop)
- Fixed some memory leaks in xml and resources deserializers.
- Various Dependencies upgraded, including:
  - AMO/TOM: 19.32
  - Power BI API: 4.2
  - MSAL: 4.40
- Build System
  - SemVer build identifiers supported with special syntax in RELEASE_NOTES.md.
  - Simplified csproj setup using a common `Directory.Build.targets` file in './src' (see: <https://docs.microsoft.com/visualstudio/msbuild/customize-your-build?#directorybuildprops-and-directorybuildtargets>).
  - Optional env setting `PBITOOLS_TempDir` supported for the _SmokeTest_ target.
  - Ensured compatibility with .Net 6 by upgrading to `fake-cli` 5.21.
- Tested with Power BI Desktop 2.100 (Dec 2021)

## 1.0.0-beta.7 - 2021-11-07
* #16 **pbi-tools Core Version** released (with distributions for Win x64, Linux Desktop x64, Linux Alpine x64). This version is available cross-platform and supports CI/CD deployment and automation scenarios where a local Power BI Desktop installation is not available.
* #16 Full/classic version of pbi-tools rebranded as "Desktop CLI" (exposed as "edition" `info` property)
* compile-pbix improvements: New `PbixWriter` API for Core version
* #48 **New 'deploy' CLI Action**, initial release limited to Report-only PBIX deployments from a PbixProj folder using service principal
* Chore: Updated dependencies (Fody, Costura.Fody, HtmlAgilityPack, Microsoft.Identity.Client, PeNet)
* Tested with Power BI Desktop 2.98 (Oct 2021)

## 1.0.0-beta.6 - 2021-10-11
* #16 Infrastructure updates for upcoming .Net 5 version
* Upgraded dependencies: CsvHelper, Fody, Costura.Fody
* Upgraded AMO/TOM libraries to 19.26
* #13 Hardened PBI Desktop API dependency: Improved forward-compatibility (IPowerBIPackage no longer statically implemented)
* #22 `export-bim`: AAS datasources no longer generated by default (opt-in via "-generateDataSources")
* #25 `info` Path of executing tool shown as _toolPath_
* Tested with Power BI Desktop 2.96, 2.97

## 1.0.0-beta.5 - 2021-05-23
* Upgraded dependencies: Newtonsoft.Json, CsvHelper, Moq, Fody, Costura.Fody, Polly
* Upgraded AMO/TOM libraries to 19.21
* PbixProj v0.9 Format
  - New Mashup serialization settings supported: `Default`, `Raw`, `Expanded`. Mode is persisted in PBIXPROJ settings, and can be provided as a command-line argument to the `extract` action.
  - **BREAKING CHANGE**: 'Expanded' is now considered legacy and no longer the default serialization mode. The `compile-pbix` action only supports projects extracted using the _Default_ or _Raw_ Mashup serialization mode.
* New action: `launch-pbi` (Only supports classic installer, not Windows Store version)
* `compile-pbix` Action
  - 'outPath' not specified: Derive from project folder
  - 'outPath' is existing file: Overwrite existing file if '-overwrite' is specified, fail otherwise
  - 'outPath' is existing directory: Generate file name from project folder, and place in directory. Fail if file exists and '-overwrite' is not specified
  - 'outPath' has extension: Assume to be file path
  - 'outPath' has no extension: Assume to be directory, generate file name from project folder
  - Sources containing a tabular model are suported.
* `extract` Action
  - #14 Support reading model from running Power BI Desktop instance. Specify port number via optional '-pbiPort' argument.
* Tested with Power BI Desktop 2.91, 2.92, 2.93

## 1.0.0-beta.4 - 2021-03-28
* PbixProj v0.8 Format
  - /Mashup extracted from V3 models (when present in PBIX)
* Fix bug in Report serializer: 64-bit id values
* Prevented `compile-pbix` from running when sources contain Model (files generated would currently be invalid)

## 1.0.0-beta.3 - 2021-03-26
* PbixProj v0.7 Format
  * Generate /Report/sections sub-folders using page index and title (e.g.: "000_Introduction")
  * Generate /Report/../visualContainers sub-folders using unique combination of visual `tabOrder`, `title`, `type`, `name` (e.g.: "00000_textbox (dbb7a)")
* FEATURE: `compile-pbix` action (EXPERIMENTAL)
  * Compile PBIX or PBIT file from PbixProj sources
* Improved diagnostic logging

## 1.0.0-beta.2 - 2021-03-21
* Upgraded AMO library to 19.18, supporting latest TMSL features (Compatibility level 1562)
* Improved documentation of CLI actions and arguments, Inserted Usage docs into README and Usage.md
* PbixProj v0.6 format: ... table/columns/*, table.dax, measure.dax, column.dax, /cultures #1 #2 #5
* Extract action: -extractFolder, -modelSerialization
* PbixProj Settings: model.serializationMode, model.ignoreProperties
* Upgraded various other dependencies
* Fix for breaking change in CsvHelper v20 API
* Added Sample project (Adventure Works DW 2020)
* Added substantial number of unit tests
* Switched 'powerbi-desktop-samples' submodule to "main" branch
* Added './pbi-tools.local.cmd'
* Using paket as dotnet local tool, removed local copy of "paket.exe"
* 'export-usage' action added
* 'extract-data' - DateTime format can be specified
* New build target: "UsageDocs"
* Added attribution to Win32 files (from projectkudu/KuduHandles)

## 1.0.0-beta.1 - 2020-11-18
* Bugfix for Power BI Desktop Nov 2020 release (2.87)
* Made implementation backwards-compatible: V3 models can be extracted with any prior version of PBI Desktop, only legacy models require the Nov 2020 version
* Upgraded AMO library to 19.12, supporting latest TMSL features
* Upgraded various other dependencies

## 1.0.0-alpha.7 - 2020-09-21
* Upgraded AMO library to 19.10, supporting latest TMSL features
* Resolve #6: In exported BIM, partition name matches table name if there is only one partition

## 1.0.0-alpha.6 - 2020-09-01
* Added extra error handling to V3 Model Feature Switch detection (enable via "Verbose" loglevel setting)
* "info" action: Added 'settings', listing pbi-tools specific environment variables -- Resolves #4
* New CLI action: "extract-data"
  * Extracts all data from a Tabular model into CSV files
  * Supports reading from PBIX file as well as from live session

## 1.0.0-alpha.5 - 2020-08-29
* Upgraded AMO library to 19.9, supporting latest TMSL features
* New CLI action: "cache"
* "info" action: Added 'pbiInstall/V3ModelEnabled'
* "cache" action: Manages the internal assembly cache for MSMDSRV (Options: List,Clear)
* Bugfix for Aug 2020 release (2.84)
* https://github.com/microsoft/powerbi-desktop-samples.git added as submodule (/data/external) for testing purposes

## 1.0.0-alpha.4 - 2020-07-21
* Upgraded AMO library to 19.6, supporting latest TMSL features
* Fixed braking change in Power BI Desktop July 2020 (2.83) version

## 1.0.0-alpha.3 - 2020-06-19
* Upgraded AMO library to 19.4.0.2, supporting latest TMSL features
* "export-bim" action
  * New argument added: `-transforms RemovePBIDataSourceVersion`
* TabularModel Serializer
  * Ignore timestamp properties in TMSL (for *.pbit files)
  * Sanitize table, hierarchy, and data source names

## 1.0.0-alpha.2 - 2020-06-10
* Improvements to "export-bim" action
  * Replaced `-generateDataSources` with `-skipDataSources` (reversed default)

## 1.0.0-alpha.1 - 2020-06-09
* Changed target framework to .Net 4.7.2 (allows compatibility with external libraries that only support .Net Standard 2.0, rather than legacy .Net Framework versions)
* Upgraded AMO library to 19.2.0.2, supporting latest TMSL features
* Upgraded various other 3rd party dependencies
* Support for new PBIX metadata format ("V3"), introduced in March 2020 version of Power BI Desktop
  * `PbixModel` API now available
* PBIXPROJ format 0.5
* New CLI action: "export-bim"
* "info" action: Added 'version', 'pbiSessions'
* Compatible with Power BI Desktop May 2020 version
* Significantly reduced exe file size by excluding 3rd party satellite assemblies
* Support for `PBITOOLS_PbiInstallDir`, `PBITOOLS_LogLevel` environment variables
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
