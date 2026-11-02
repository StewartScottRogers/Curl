# Azure Storage requirement matrix: Data Lake Storage Gen2

Curl's `abfs://` and `abfss://` client (`Curl.Extended.Protocol.AzureStorage.UnitLibrary`, ADR-1105) measured against
the Azure Data Lake Storage Gen2 REST API at `x-ms-version: 2025-01-05` (Path - Read, Path - Create, Path - Update,
Path - List, the error body and the ADLS Gen2 URI), by the cases in
`Curl.Conformance.Extended.UnitTests/AzureStorage/DataLake/` against `AzureDataLakeReferenceEndpoint`. The Blob
service's requirements, among them the version, date and bearer header every request carries, are in
[AzureStorage.md](AzureStorage.md).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| AzureStorage:PathRead-1 | REQUIRED | Path - Read is `GET` on `/<filesystem>/<path>` at the account's DFS host; the response body is the file's committed bytes. | AzureDataLakeTransferCases.Read_AbfssBareAccount_GetsThePathFromTheAccountsDfsHost |
| AzureStorage:DfsUri-1 | REQUIRED | `abfs[s]://<filesystem>@<account>.dfs.core.windows.net/<path>` names `<path>` in `<filesystem>` on that host. | AzureDataLakeTransferCases.Read_AbfsFullDfsHost_GetsThePathFromThatHost |
| AzureStorage:DfsUri-2 | MAY | A bare account name in the authority stands for `<account>.dfs.core.windows.net` (ADR-1105). | AzureDataLakeTransferCases.Read_AbfssBareAccount_GetsThePathFromTheAccountsDfsHost |
| AzureStorage:DfsErrors-1 | MUST | A failed DFS operation answers a 4xx or 5xx status with a JSON body `{"error":{"code":...,"message":...}}`; curl exits 22 naming the code and message. | AzureDataLakeTransferCases.Read_MissingPath_ReadsTheJsonErrorBodyAndExits22 |
| AzureStorage:PathCreate-1 | REQUIRED | Path - Create of a file is `PUT` on `/<filesystem>/<path>?resource=file`. | AzureDataLakeUploadAndListingCases.Upload_EightMiBAndMore_CreatesAppendsAtEachPositionThenFlushesTheLength |
| AzureStorage:PathUpdate-1 | REQUIRED | Path - Update `action=append` is `PATCH` with the data as its body and `position` the offset it goes at: the length of the data appended before it. | AzureDataLakeUploadAndListingCases.Upload_EightMiBAndMore_CreatesAppendsAtEachPositionThenFlushesTheLength |
| AzureStorage:PathUpdate-2 | REQUIRED | Path - Update `action=flush` is `PATCH` with no body and `position` equal to the file's length after all appended data; it commits that data. | AzureDataLakeUploadAndListingCases.Upload_EightMiBAndMore_CreatesAppendsAtEachPositionThenFlushesTheLength |
| AzureStorage:PathUpdate-3 | REQUIRED | An empty file is created and flushed at `position=0` with no append. | AzureDataLakeUploadAndListingCases.Upload_EmptyFile_CreatesThenFlushesAtPositionZeroWithoutAnAppend |
| AzureStorage:PathList-1 | REQUIRED | Path - List is `GET` on `/<filesystem>?resource=filesystem` with `recursive` and, for a subdirectory, `directory`; the body's `paths` array names each entry, a directory with `isDirectory`. | AzureDataLakeUploadAndListingCases.List_ThreePages_FollowsEachContinuationAndPrintsEveryName |
| AzureStorage:PathList-2 | REQUIRED | An `x-ms-continuation` response header asks for the next page by passing its value as `continuation`; its absence ends the listing. | AzureDataLakeUploadAndListingCases.List_ThreePages_FollowsEachContinuationAndPrintsEveryName |
| AzureStorage:PathCreate-2 | OPTIONAL | Path - Create of a directory is `PUT` with `resource=directory`. | n/a: curl's client creates no directories; an upload names a file |
| AzureStorage:PathLease-1 | OPTIONAL | Path - Lease acquires, renews and releases a lease, sent as `x-ms-lease-id` on later calls. | n/a: curl's client takes no lease |
| AzureStorage:PathDelete-1 | OPTIONAL | Path - Delete is `DELETE` on `/<filesystem>/<path>`. | n/a: curl's client does not delete |
