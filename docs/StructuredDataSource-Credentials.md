# Setting credentials for Structured Data Sources

## References

* <https://docs.microsoft.com/dotnet/api/microsoft.analysisservices.tabular.credential?view=analysisservices-dotnet>
* <https://docs.microsoft.com/dotnet/api/microsoft.analysisservices.tabular.authenticationkind?view=analysisservices-dotnet>
* <https://docs.microsoft.com/power-query/handlingauthentication>
* <https://docs.microsoft.com/azure/analysis-services/analysis-services-datasource>
* <https://docs.microsoft.com/analysis-services/tmsl/datasources-object-tmsl?view=asallproducts-allversions>
* <https://docs.microsoft.com/openspecs/sql_server_protocols/ms-ssas-t/ee12dcb7-096e-4e4e-99a4-47caeb9390f5>

## Articles

* <https://github.com/MicrosoftDocs/azure-docs/issues/6226>
* <https://www.powershellgallery.com/packages/DeployCube/1.0.0.3226/Content/public%5CUpdate-TabularCubeDataSource.ps1>
* <https://stackoverflow.com/questions/56722400/how-do-i-add-a-structured-odbc-data-source-to-a-tabular-model-using-the-amo-tabu>
* <https://stackoverflow.com/questions/57407809/authenticate-azure-blob-storage-account-in-cloud-using-a-runbook>
* <https://blog.gbrueckl.at/2017/11/processing-azure-analysis-services-oauth-sources-like-azure-data-lake-store/>

## Typical *Credential* properties

* `AuthenticationKind` *(Windows, UsernamePassword, ServiceAccount, OAuth2, Implicit, Key)*
* `kind` *(SQL, DataLake, File)*
* `path`
* `Username`, `Password`
* `EncryptConnection`
* `PrivacySetting` *(Organizational)*

## Examples
