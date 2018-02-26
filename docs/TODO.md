
## Core
- [x] CLI - PowerArgs
- [x] Model (transforms)
  - [x] Partitions
- [x] Mashup
- [x] Resources
- [x] Report
- [ ] Version
- [ ] Connections
- [ ] DataModelSchema "Model (Schema)" -- pbit

## Required for first release
- [ ] FIX: ProjectFolder deletes all contents if unhandled exception occurs before any files have been written
- [x] convert idCache file to .pbixproj settings file
- [x] lower .Net requirement (4.5 - included since Windows 8; same version as PBI Desktop)
- [x] dataSource-lookup merge
- [ ] *** discover paths (msmdsrv.exe, Store install, etc) -- hard, but important
- [ ] *** Sanitize filenames (impl in ProjectFolder, make reversible)
- [ ] *** Distribution story (chocolatey, bootstrapper)
- [ ] * tool versioning
- [ ] pbixproj component
- [ ] write unhandled exception details to error file

## Additional features
- [ ] ilmerge all dependencies ??
- [ ] trace deleted files
- [ ] live connection (DataModel|DataModelSchema null, Connections)
- [ ] PBIT (DataModelSchema)
- [ ] Convert to PBIT (for source control) -- make option
- [ ] Integration with Tabular Editor
- [ ] support all M escape sequences
- [ ] Model -noTransform mode to show off effect of transformations

## Meta
- [ ] TESTS
- [ ] SAMPLES
- [ ] README
- [ ] DOCS


## v.Next (in order of priority)
- [ ] *** PBIX Compiler
- [ ] Tabular Editor SaveAs-Folder format (incl attribute: SerialisationSettings)
- [ ] Host pbix dataset (run DAX queries from SSMS, DAX Studio) -- sync/async
- [ ] integrate with gitlib (auto-commit)
- [ ] AAS Support (convert to bim?)
- [ ] Refresh data
- [ ] Use local credentials (from PBI Desktop)
