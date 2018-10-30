# `Serialization` namespace

Contains specific serializers for the various PowerBIPackage parts (Mashup, Model, ...) that can convert between the raw data representation in the PBIX file and the PbixProj extracted file format.

1. Serialize

- `PowerBIPackage part` --Serialize--> `PbixProj folder`

2. Deserialize

- `PowerBIPackage part` <--Deserialize-- `PbixProj folder`
- Should happen at granularity of PowerBIPackage (for instance, MashupData requires: partsBytes, permissionBytes, metadataBytes)
- TODO: Create `PbixWriter` component to perform reverse action of `PbixReader`

## Relationship to other components

- `PbixReader` extracts parts from a PowerBIPackage and converts those to common data structures (JObject, XDocument, String, ...)
- `PbixExtractAction` uses an intance of `PbixReader` as well as serializers from this namespace to extract an entire PBIX file to a folder on disk