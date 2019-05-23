# `Serialization` Namespace

Contains specific serializers for the various PowerBIPackage parts (Mashup, Model, ...) that can convert between the raw data representation in the PBIX file and the PbixProj extracted file format.

1. Serialize

   - `PowerBIPackage part` -- *Serialize* --> `PbixProj folder`

2. Deserialize

   - `PowerBIPackage part` <-- *Deserialize* -- `PbixProj folder`
   - Should happen at granularity of PowerBIPackage (for instance, MashupData requires: partsBytes, permissionBytes, metadataBytes)
   - TODO: Create `PbixWriter` component to perform reverse action of `PbixReader`
     - `PbixReader` ctor: *IPowerBIPackage*
       - `ReadReport() : JObject` ...
     - `PbixWriter` ctor: Func<>
       - `WritePbix()` -- [1] NEW, [2] Merge
         - Args: path, format
   - Ex: Mashup
     - Create parts|permission|metadataBytes from PbixProj sources
       - Package: **ZipArchive** (PbixReader) - /Package folder (MashupSerializer)
       - Metadata: **XDocument** (PbixReader) - /Metadata folder (MashupSerializer)
       - Permissions: **JObject** (PbixReader)
       - -> MashupSerializer performs transformation, PbixReader extracts raw data
       - !! **Need clean separation between DATA EXTRACTION (into raw JObject, XDOcument, etc...) and PbixProj CONVERSION**
         - Some PbixProj logic is inside ExtractAction, other inside MashupSerializer
           - Plain extraction:
             - Version, Connections, ReportMetadata, ReportSettings, DiagramViewState, LinguisticSchema
           - With conversion:
             - **Mashup, Report, Model**
             - CustomVisuals, StaticResources
         - Reversing PbixProj conversions should be in same place
           - **TODO** Create `ReportSerializer`, `ResourcesSerializer`, `JsonPartSerializer`, `XmlPartSerializer`, `TextPartSerializer`
         - -> ***XxxSerializers implement PbixProj format, PbixReader only handles raw data formats, ExtractAction merely coordinates different serializers***
         - `TabularModelSerializer` to be aware of different serialization formats (*Tabular Editor* compat, for instance)
     - Convert to Part blob/stream
     - Provide to PbixWriter

## Relationship to other components

- `PbixReader` extracts parts from a *IPowerBIPackage* and converts those to common data structures (JObject, XDocument, String, ...), using PowerBIPartConverters
- `PbixExtractAction` uses an intance of `PbixReader` as well as serializers from this namespace to extract an entire PBIX file to a folder on disk
- `PbixWriter` writes out a *IPowerBIPackage*, with each of its parts provided in a common data format (JObject, XDocument, ...)
  - Base implementation: Take Func for each part `Func<IStreamablePowerBIPackagePartContent>`, special handling for *CustomVisuals* and *StaticResources*
  - *PowerBIPackage* impementation: Pass from existing file
  - *PbixProj* implementation: Convert from sources
  - Parts:
    - [ ] 1 Mashup (TODO)
    - [ ] 2 DataModel, DataModelSchema (mostly done)
    - [x] 3 Version, 4 Connections, 5 LinguisticSchema (easy)
    - [x] 6 ReportSettings, 7 ReportMetadata (easy)
    - [ ] 8 ReportDocument (TODO)
    - [ ] 9 CustomVisuals, 10 StaticResources (easy)
    - [x] 11 DiagramViewState, 
	- [ ] 12 DiagramLayout
- `PbixCompileAction` - Generates a new PBIX/T file from PbixProj sources
  - format:pbix|pbit
- `PbixMergeAction` - Updates a PBIX/T file from PbixProj sources (use to refresh PBIX working copy from repo ... TODO test feasibility)
  - partSource: pbix|folder|pipe
  - dest: *PBIX path*
  - include|excludeParts