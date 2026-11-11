# Google Cloud Storage XML API conformance (part 1: object operations)

Curl's `gs://` client (`Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary`) measured against the Cloud Storage
XML API (cloud.google.com/storage/docs/xml-api): GET Object, PUT Object, List Objects, the XML API error responses,
the request headers, path-style requests to `storage.googleapis.com`, OAuth 2.0 bearer and anonymous requests, and the
`STORAGE_EMULATOR_HOST` endpoint override (BL-4475). V4 HMAC signing is part 2, in
[GoogleCloudStorage-Signing.md](GoogleCloudStorage-Signing.md) (BL-4476).

The reference endpoint is `Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/GcsReferenceServer`; the cases
are in `Curl.Conformance.Extended.UnitTests/GoogleCloudStorage/`. IDs are `GoogleCloudStorage:XMLAPI-<page>-<n>`, the
locator naming the XML API reference page.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| GoogleCloudStorage:XMLAPI-Endpoint-1 | REQUIRED | Requests go to `storage.googleapis.com` over HTTPS, path-style (`/bucket/object`). | GcsXmlApiObjectCases.Download_GetsThePathStyleObjectOverHttps |
| GoogleCloudStorage:XMLAPI-Headers-Host-1 | REQUIRED | Every request carries the `Host` header naming the endpoint. | GcsXmlApiObjectCases.Download_GetsThePathStyleObjectOverHttps |
| GoogleCloudStorage:XMLAPI-Headers-ContentLength-1 | REQUIRED | A request with a body that is not chunked carries `Content-Length`. | GcsXmlApiObjectCases.Upload_PutsTheFileAsTheObjectWithItsLength |
| GoogleCloudStorage:XMLAPI-Headers-Date-1 | REQUIRED | `Date` (or `x-goog-date`) dates a request signed with an HMAC key. | n/a: only signed requests need it; bearer and anonymous requests are unsigned, and signing is part 2 (BL-4476) |
| GoogleCloudStorage:XMLAPI-GetObject-1 | REQUIRED | GET Object is `GET /bucket/object` and the body is the object's data. | GcsXmlApiObjectCases.Download_GetsThePathStyleObjectOverHttps |
| GoogleCloudStorage:XMLAPI-PutObject-1 | REQUIRED | PUT Object is `PUT /bucket/object` with the object's data as the body. | GcsXmlApiObjectCases.Upload_PutsTheFileAsTheObjectWithItsLength |
| GoogleCloudStorage:XMLAPI-RequestURI-1 | MUST | Object names are percent-encoded in the request URI. | GcsXmlApiObjectCases.Download_PercentEncodesTheObjectName |
| GoogleCloudStorage:XMLAPI-ListObjects-1 | REQUIRED | List Objects is `GET /bucket` with `list-type=2` and the body is a `ListBucketResult`. | GcsXmlApiListCases.List_WithDelimiterOverPages_FollowsTheContinuationToken |
| GoogleCloudStorage:XMLAPI-ListObjects-2 | REQUIRED | `prefix` limits the names; with `delimiter`, names past it roll up into `CommonPrefixes`. | GcsXmlApiListCases.List_WithDelimiterOverPages_FollowsTheContinuationToken |
| GoogleCloudStorage:XMLAPI-ListObjects-3 | REQUIRED | A truncated listing (`IsTruncated` true) continues with `continuation-token` set to `NextContinuationToken`. | GcsXmlApiListCases.List_WithDelimiterOverPages_FollowsTheContinuationToken |
| GoogleCloudStorage:XMLAPI-ListObjects-4 | REQUIRED | A truncated listing that names the next page by `NextMarker` continues from that name. | GcsXmlApiListCases.List_NextMarkerOverSeveralPages_SendsTheMarkerBack |
| GoogleCloudStorage:XMLAPI-Errors-1 | REQUIRED | A failure answers an error status with an `Error` body (`Code`, `Message`, `Details`); curl fails with exit 22 naming it. | GcsXmlApiObjectCases.Download_ErrorBody_ExitsWith22AndNamesTheCode |
| GoogleCloudStorage:XMLAPI-OAuth2-1 | REQUIRED | OAuth 2.0 authentication sends `Authorization: Bearer <access token>`. | GcsXmlApiObjectCases.Download_OAuth2Bearer_SendsTheAccessToken |
| GoogleCloudStorage:XMLAPI-Anonymous-1 | REQUIRED | A request without credentials carries no `Authorization` header; a private bucket refuses it with `401`. | GcsXmlApiObjectCases.Download_NoCredentials_SendsNoAuthorizationAndIsRefusedByAPrivateBucket |
| GoogleCloudStorage:XMLAPI-Emulator-1 | SHOULD | `STORAGE_EMULATOR_HOST` replaces the endpoint, as Google's client libraries read it. | GcsXmlApiObjectCases.Download_StorageEmulatorHost_SendsTheRequestToTheEmulator |

## Request methods

`gs://` sends only the methods its operations need: GET to download or list, and with `-T` PUT. `-X GET` without `-T` and `-X PUT` with it are accepted, since they name what is sent anyway. `-I`/`--head` and every other `-X` (`DELETE`, `HEAD`, `POST`, ...) fail with exit 4 and `gs:// does not support ...` before any connection, so a destructive method is never reported done while a GET was sent instead (ADR-1249, `GcsRequestMethodTests`).
