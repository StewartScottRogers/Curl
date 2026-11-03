# Google Cloud Storage V4 HMAC signing conformance (part 2: signing)

Curl's `gs://` client (`Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary`) signing with an HMAC key
(`--user ACCESS_ID:SECRET`) measured against Cloud Storage's "V4 signing process"
(cloud.google.com/storage/docs/authentication/signatures, with its canonical-requests page) in the `Authorization`
header form (BL-4476). Part 1, the XML API itself, is [GoogleCloudStorage.md](GoogleCloudStorage.md).

The verifier is `Curl.Conformance.Extended.UnitLibrary/GoogleCloudStorage/Signing/GcsV4HmacReferenceAuthorizer`,
written from the documentation with BCL `HMACSHA256` and `SHA256`, never from the client's `Signing/` code, and pinned
by a vector computed by hand from the documented steps (`GcsV4HmacReferenceAuthorizerTests`). The cases are in
`Curl.Conformance.Extended.UnitTests/GoogleCloudStorage/Signing/` and run at a fixed clock, 2026-11-02 10:30:00 UTC.
IDs are `GoogleCloudStorage:V4Signing-<step>-<n>`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| GoogleCloudStorage:V4Signing-Algorithm-1 | REQUIRED | An HMAC-key signature uses the algorithm `GOOG4-HMAC-SHA256`. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-Authorization-1 | REQUIRED | The `Authorization` header is `GOOG4-HMAC-SHA256 Credential=<access id>/<scope>, SignedHeaders=<names>, Signature=<hex>`. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-Date-1 | REQUIRED | `x-goog-date` carries the request's UTC time as `yyyyMMdd'T'HHmmss'Z'`, the same time the string to sign and the scope's date use. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-Date-2 | REQUIRED | The request reaches Cloud Storage within 15 minutes of its `x-goog-date`. | n/a: a server-side check of the client's clock; the client only stamps the time it is given (V4Signing-Date-1) |
| GoogleCloudStorage:V4Signing-Scope-1 | REQUIRED | The credential scope is `<date>/<region>/storage/goog4_request`, region `auto` when none is named. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-Scope-2 | REQUIRED | A named region (such as `us-east1`) is the scope's region and a step of the signing key. | GcsV4SigningCases.Download_WithHmacKeyAndARegion_SignsWithThatRegionsScope |
| GoogleCloudStorage:V4Signing-CanonicalRequest-1 | REQUIRED | The canonical request is the method, the canonical URI, the canonical query, the canonical headers, a blank line, the signed headers and the payload, each on its own line. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-CanonicalURI-1 | REQUIRED | The canonical URI is the path percent-encoded as UTF-8 with upper-case hex, keeping only `A-Z a-z 0-9 - . _ ~` and `/`. | GcsV4SigningCases.Download_WithHmacKey_SignsTheReservedAndNonAsciiNamePercentEncoded |
| GoogleCloudStorage:V4Signing-CanonicalQuery-1 | REQUIRED | The canonical query is every parameter `name=value`, both percent-encoded, sorted by name and joined by `&`. | GcsV4SigningCases.List_WithHmacKey_SignsTheQuerySortedByName |
| GoogleCloudStorage:V4Signing-CanonicalHeaders-1 | REQUIRED | Canonical headers are `name:value` lines, names lower case and sorted, values trimmed with inner whitespace collapsed. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-SignedHeaders-1 | REQUIRED | Signed headers are the canonical header names joined by `;`; `host` and every `x-goog-*` header sent are signed. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-Payload-1 | REQUIRED | The payload line is the body's lower-case hex SHA-256, sent as `x-goog-content-sha256` (or `UNSIGNED-PAYLOAD`). | GcsV4SigningCases.Upload_WithHmacKey_SignsTheBodysSha256 |
| GoogleCloudStorage:V4Signing-StringToSign-1 | REQUIRED | The string to sign is the algorithm, the timestamp, the scope and the hex SHA-256 of the canonical request, each on its own line. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-SigningKey-1 | REQUIRED | The signing key is HMAC-SHA256 chained from `GOOG4<secret>` over the date, region, `storage` and `goog4_request`; the signature is the hex HMAC of the string to sign. | GcsV4SigningCases.Download_WithHmacKey_SignsTheGetWithGoog4HmacSha256 |
| GoogleCloudStorage:V4Signing-Signature-1 | REQUIRED | A signature that does not verify is refused with `403 SignatureDoesNotMatch`; curl fails with exit 22 naming it. | GcsV4SigningCases.Download_WithTheWrongSecret_IsRefusedAndExitsWith22 |
| GoogleCloudStorage:V4Signing-QueryString-1 | OPTIONAL | The query-string form (signed URLs, `X-Goog-Signature`) carries the signature in the URL. | n/a: curl signs each request it sends in the `Authorization` header form and makes no signed URLs |
| GoogleCloudStorage:V4Signing-RSA-1 | OPTIONAL | Service-account keys sign with `GOOG4-RSA-SHA256`. | n/a: `--user` carries an HMAC key; RSA service-account signing is not a `gs://` credential (ADR-1099) |
