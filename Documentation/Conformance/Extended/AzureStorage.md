# Azure Storage requirement matrix

Curl's `az://` client (`Curl.Extended.Protocol.AzureStorage.UnitLibrary`) measured against the Azure Storage Blob
service REST API at `x-ms-version: 2025-01-05` and HTTP/1.1 (RFC 9112), by the cases in
`Curl.Conformance.Extended.UnitTests/AzureStorage/` against `AzureBlobReferenceEndpoint`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| AzureStorage:GetBlob-1 | REQUIRED | Get Blob is `GET` on `/<container>/<blob>` with `Host`, `x-ms-date` and `x-ms-version`; the response body is the blob. | AzureBlobTransferCases.Download_BearerToken_GetsTheBlobWithVersionDateAndAuthorization |
| AzureStorage:Versioning-1 | REQUIRED | An authorized request names the service version in `x-ms-version` (2017-11-09 or later for OAuth). | AzureBlobTransferCases.Download_BearerToken_GetsTheBlobWithVersionDateAndAuthorization |
| AzureStorage:AuthorizeOAuth-1 | MUST | A Microsoft Entra request carries `Authorization: Bearer <token>`. | AzureBlobTransferCases.Download_BearerToken_GetsTheBlobWithVersionDateAndAuthorization |
| AzureStorage:RFC9112-7.1-1 | MUST | A client reads a chunked response body to its last chunk. | AzureBlobTransferCases.Download_BearerToken_GetsTheBlobWithVersionDateAndAuthorization |
| AzureStorage:PutBlob-1 | REQUIRED | Put Blob is `PUT` on `/<container>/<blob>` with `x-ms-blob-type: BlockBlob`. | AzureBlobTransferCases.Upload_SmallFile_SendsOnePutBlobOfABlockBlob |
| AzureStorage:PutBlob-2 | REQUIRED | Put Blob carries `Content-Length`, the length of the body that becomes the blob. | AzureBlobTransferCases.Upload_SmallFile_SendsOnePutBlobOfABlockBlob |
| AzureStorage:ErrorCodes-1 | MUST | A failed operation answers a 4xx or 5xx status with an `Error` body holding `Code` and `Message`; curl exits 22 naming them. | AzureBlobTransferCases.Download_MissingBlob_ReadsTheErrorBodyAndExits22 |
| AzureStorage:AnonymousAccess-1 | MAY | A read of a container with anonymous read access needs no `Authorization` and no SAS. | AzureBlobTransferCases.Download_NoCredential_SendsNoAuthorizationAndReadsAPublicContainer |
| AzureStorage:AuthorizeSharedKey-1 | REQUIRED | A Shared Key request signs its canonical headers and resource in `Authorization: SharedKey`. | n/a: curl's client does not implement Shared Key (BL-4290's Notes) |
