# S3 requirement matrix (part 2 of 2: AWS Signature Version 4 signing)

Curl's `s3://` client (`Curl.Extended.Protocol.S3.UnitLibrary`) signing requests measured against the Amazon S3 API
Reference's "Authenticating Requests (AWS Signature Version 4)" and its "Signature Calculations for the Authorization
Header: Transferring Payload in a Single Chunk" page (BL-4499). Part 1, the REST operations, is [S3.md](S3.md).

The verifier is `Curl.Conformance.Extended.UnitLibrary/S3/Signing/S3SigV4ReferenceAuthorizer` (with
`S3SigV4CanonicalRequest`), written from the documentation with BCL `HMACSHA256` and `SHA256`, never from the client's
`Signing/` code, and plugged into `S3ReferenceEndpoint` through `IS3RequestAuthorizer`. `S3SigV4ReferenceAuthorizerTests`
reproduces the page's published GET Object, PUT Object and ListObjects signatures byte for byte. The cases are in
`Curl.Conformance.Extended.UnitTests/S3/Signing/` and run at the examples' time, 2013-05-24 00:00:00 UTC, with their
access key `AKIAIOSFODNN7EXAMPLE`; each case asserts the `Authorization` header byte for byte against the verifier's
own computation. The client cannot send the examples' exact requests (it sends `Host: bucket.s3.<region>.amazonaws.com`,
no `Range`, `Date` or `x-amz-storage-class`, and lists with ListObjectsV2), so the cases send the nearest request the
command line makes (ADR-1211). IDs are `S3:SigV4-<section>-<n>`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| S3:SigV4-Authorization-1 | REQUIRED | The algorithm is `AWS4-HMAC-SHA256`. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-Authorization-2 | REQUIRED | The `Authorization` header is `AWS4-HMAC-SHA256 Credential=<access key>/<scope>,SignedHeaders=<names>,Signature=<hex>`. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-Date-1 | REQUIRED | `x-amz-date` carries the request's UTC time as `yyyyMMdd'T'HHmmss'Z'`, the time the string to sign and the scope's date use. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-Date-2 | REQUIRED | The request reaches S3 within 15 minutes of its timestamp. | n/a: a server-side check of the client's clock; the client stamps the time it is given (SigV4-Date-1) |
| S3:SigV4-Scope-1 | REQUIRED | The credential scope is `<date>/<region>/s3/aws4_request`. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-Scope-2 | REQUIRED | The region the request is sent to is the scope's region and a step of the signing key. | S3SigV4SigningCases.Download_InAnotherRegion_SignsWithThatRegionsScope |
| S3:SigV4-CanonicalRequest-1 | REQUIRED | The canonical request is the method, canonical URI, canonical query string, canonical headers, signed headers and hashed payload, each on its own line. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-CanonicalURI-1 | REQUIRED | The canonical URI is the absolute path UriEncoded: every byte but `A-Z a-z 0-9 - . _ ~` and `/` as `%XY`, upper-case hex. | S3SigV4SigningCases.Upload_ThePutObjectExample_SignsTheBodysSha256 |
| S3:SigV4-CanonicalURI-2 | REQUIRED | S3 encodes the object key once (no double encoding), UTF-8 bytes for non-ASCII characters. | S3SigV4SigningCases.Download_KeyWithReservedSpaceAndNonAsciiCharacters_IsSignedEncodedOnce |
| S3:SigV4-CanonicalQuery-1 | REQUIRED | The canonical query string is every parameter `name=value`, both UriEncoded (`/` too), sorted by name and joined by `&`. | S3SigV4SigningCases.List_TheListObjectsExample_SignsSeveralParametersSortedWithAnEmptyValue |
| S3:SigV4-CanonicalQuery-2 | REQUIRED | A parameter without a value is `name=` with an empty value. | S3SigV4SigningCases.List_TheListObjectsExample_SignsSeveralParametersSortedWithAnEmptyValue |
| S3:SigV4-CanonicalHeaders-1 | REQUIRED | Canonical headers are `name:value` lines, names lower case and sorted, values trimmed with runs of spaces collapsed. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-SignedHeaders-1 | REQUIRED | Signed headers are the canonical header names joined by `;`; `host`, `x-amz-content-sha256` and every `x-amz-*` header sent are signed. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-Payload-1 | REQUIRED | `x-amz-content-sha256` is sent and is the hashed payload line: the payload's lower-case hex SHA-256 (empty: `e3b0...b855`). | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-Payload-2 | REQUIRED | A request with a body hashes that body. | S3SigV4SigningCases.Upload_ThePutObjectExample_SignsTheBodysSha256 |
| S3:SigV4-Payload-3 | OPTIONAL | `UNSIGNED-PAYLOAD` in place of the hash leaves the payload unsigned. | n/a: the client always hashes the body it sends (`S3Client`); the verifier accepts `UNSIGNED-PAYLOAD` (S3SigV4ReferenceAuthorizerTests) |
| S3:SigV4-StringToSign-1 | REQUIRED | The string to sign is the algorithm, the timestamp, the scope and the hex SHA-256 of the canonical request, each on its own line. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-SigningKey-1 | REQUIRED | The signing key is HMAC-SHA256 chained from `AWS4<secret>` over the date, region, `s3` and `aws4_request`; the signature is the hex HMAC of the string to sign. | S3SigV4SigningCases.Download_TheGetObjectExample_IsSignedWithTheExampleKeyAndTime |
| S3:SigV4-SessionToken-1 | REQUIRED | Temporary credentials send their session token as `x-amz-security-token`, a signed header. | S3SigV4SigningCases.Download_WithASessionToken_SignsTheSecurityTokenHeader |
| S3:SigV4-Signature-1 | REQUIRED | A signature that does not verify is refused with `403 SignatureDoesNotMatch`; curl fails with exit 22 naming it. | S3SigV4SigningCases.Download_WithTheWrongSecret_IsRefusedAndExitsWith22 |
| S3:SigV4-QueryString-1 | OPTIONAL | Query-string authentication (presigned URLs, `X-Amz-Signature`) carries the signature in the URL. | n/a: the client signs each request in the `Authorization` header and makes no presigned URLs |
| S3:SigV4-Chunked-1 | OPTIONAL | A payload sent in chunks (`STREAMING-AWS4-HMAC-SHA256-PAYLOAD`) signs each chunk. | n/a: the client sends every payload, multipart parts included, in a single chunk |
