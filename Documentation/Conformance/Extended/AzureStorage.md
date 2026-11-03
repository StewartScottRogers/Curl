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
| AzureStorage:PutBlock-1 | REQUIRED | Put Block is `PUT` on `/<container>/<blob>?comp=block&blockid=<id>` with `Content-Length` and the block as its body. | AzureBlobBlockUploadAndListingCases.Upload_EightMiBAndMore_SendsEqualLengthBlocksThenTheBlockListInOrder |
| AzureStorage:PutBlock-2 | MUST | Every block ID of a blob is Base64 and of the same length before encoding. | AzureBlobBlockUploadAndListingCases.Upload_EightMiBAndMore_SendsEqualLengthBlocksThenTheBlockListInOrder |
| AzureStorage:PutBlockList-1 | REQUIRED | Put Block List is `PUT` with `comp=blocklist` and a `BlockList` body naming the staged blocks in the order that makes up the blob. | AzureBlobBlockUploadAndListingCases.Upload_EightMiBAndMore_SendsEqualLengthBlocksThenTheBlockListInOrder |
| AzureStorage:ListBlobs-1 | REQUIRED | List Blobs is `GET` on `/<container>?restype=container&comp=list`. | AzureBlobBlockUploadAndListingCases.List_ThreePages_FollowsEachNextMarkerAndPrintsEveryName |
| AzureStorage:ListBlobs-2 | REQUIRED | A non-empty `NextMarker` asks for the next page by passing it as `marker`; an empty one ends the listing. | AzureBlobBlockUploadAndListingCases.List_ThreePages_FollowsEachNextMarkerAndPrintsEveryName |
| AzureStorage:CredentialPrecedence-1 | MUST | `--oauth2-bearer` authorizes the request even when `AZURE_STORAGE_BEARER_TOKEN` and `AZURE_STORAGE_SAS_TOKEN` are set (the extension's documented order; the service defines none). | AzureBlobCredentialAndFramingCases.Download_OAuth2BearerAndBothVariables_SendsTheOptionsToken |
| AzureStorage:CredentialPrecedence-2 | MUST | Without `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN` authorizes the request ahead of `AZURE_STORAGE_SAS_TOKEN`. | AzureBlobCredentialAndFramingCases.Download_BearerVariableAndSasVariable_SendsTheBearerVariable |
| AzureStorage:CredentialPrecedence-3 | MUST | With no bearer token, `AZURE_STORAGE_SAS_TOKEN` (with or without its leading `?`) authorizes the request. | AzureBlobCredentialAndFramingCases.Download_SasVariableAlone_AppendsTheTokenToTheQueryAndSendsNoAuthorization |
| AzureStorage:AuthorizeSas-1 | REQUIRED | A shared access signature request carries the token's parameters, `sig` among them, in the query and no `Authorization`. | AzureBlobCredentialAndFramingCases.Download_SasVariableAlone_AppendsTheTokenToTheQueryAndSendsNoAuthorization |
| AzureStorage:RFC9112-7.1-2 | MUST | A chunked body that is only the last chunk is an empty body; the client reads it and the transfer succeeds. | AzureBlobCredentialAndFramingCases.Download_ChunkedEmptyBlob_WritesNothingAndExits0 |
| AzureStorage:RFC9112-6.3-1 | MUST | A response with neither `Content-Length` nor `Transfer-Encoding` has a body that ends when the server closes the connection. | AzureBlobCredentialAndFramingCases.Download_CloseDelimitedBody_ReadsToTheClose |
| AzureStorage:RFC9112-6.3-2 | MUST | A close-delimited `Error` body is read to the close, and curl exits 22 naming its `Code` and `Message`. | AzureBlobCredentialAndFramingCases.Download_CloseDelimitedErrorBody_ReadsTheErrorAndExits22 |
| AzureStorage:RFC9112-3.2-1 | MUST | A client sends a `Host` header field in every HTTP/1.1 request, naming the account's Blob host. | AzureBlobRequestHeaderCases.List_SasVariable_SendsHostVersionDateAndTheTokenAfterTheListQuery |
| AzureStorage:RequestHeaders-1 | REQUIRED | Every operation's request carries `x-ms-date` (or `Date`) as an RFC 1123 time in GMT, and `x-ms-version`, whichever credential authorizes it, an anonymous or SAS request included. | AzureBlobRequestHeaderCases.List_SasVariable_SendsHostVersionDateAndTheTokenAfterTheListQuery |
| AzureStorage:AuthorizeSas-2 | REQUIRED | A SAS token's parameters join the operation's own query parameters (`restype`, `comp`, `marker`) in the one query string. | AzureBlobRequestHeaderCases.List_SasVariable_SendsHostVersionDateAndTheTokenAfterTheListQuery |
| AzureStorage:PutBlob-3 | REQUIRED | Put Blob of an empty file carries `Content-Length: 0` and creates an empty block blob. | AzureBlobRequestHeaderCases.Upload_EmptyFile_SendsOnePutBlobWithContentLengthZero |
| AzureStorage:PutBlockList-2 | REQUIRED | Put Block List carries `Content-Length`, the length of its `BlockList` body. | AzureBlobRequestHeaderCases.Upload_EightMiBAndMoreWithBearer_FramesEveryBodyByContentLengthOverTls |
| AzureStorage:PutBlock-3 | MUST | A block ID is at most 64 bytes before Base64 encoding. | AzureBlobRequestHeaderCases.Upload_EightMiBAndMoreWithBearer_FramesEveryBodyByContentLengthOverTls |
| AzureStorage:PutBlock-4 | MUST NOT | A blob is not committed from more than 50,000 blocks. | n/a: at the client's 8 MiB block that is an upload of over 390 GiB, beyond what a fast case can send; the client grows the block or ends with exit 63 first (ADR-1213), pinned by the unit test AzureBlobOperationsTests.BlockSizeFor_UploadThatNeedsMoreThan50000BlocksOf8MiB_GrowsTheBlockToStayWithin50000 |
| AzureStorage:RFC9112-6.1-1 | MUST NOT | A sender does not send `Content-Length` in a message that carries `Transfer-Encoding`; every request body here is framed by `Content-Length` alone. | AzureBlobRequestHeaderCases.Upload_EightMiBAndMoreWithBearer_FramesEveryBodyByContentLengthOverTls |
| AzureStorage:AuthorizeOAuth-2 | MUST | A request authorized with OAuth is sent over HTTPS. | AzureBlobRequestHeaderCases.Upload_EightMiBAndMoreWithBearer_FramesEveryBodyByContentLengthOverTls |
| AzureStorage:ListBlobs-3 | OPTIONAL | `maxresults` limits a page's size; without it the service chooses (up to 5,000). | n/a: curl's client sends no `maxresults` and follows `NextMarker` whatever the page size |
| AzureStorage:GetBlob-2 | OPTIONAL | `Range` or `x-ms-range` reads part of a blob. | n/a: curl's client reads the whole blob |

Data Lake Storage Gen2 (`abfs://`, `abfss://`) has its own matrix: [AzureStorage-DataLake.md](AzureStorage-DataLake.md).
