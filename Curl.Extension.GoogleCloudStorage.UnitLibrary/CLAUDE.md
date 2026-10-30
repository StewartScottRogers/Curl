# Curl.Extension.GoogleCloudStorage.UnitLibrary

The Google Cloud Storage extension (ADR-0999), for `gs://` URLs over the Cloud Storage XML API.
Namespace `Curl.Extension.GoogleCloudStorage`. It references the base class library and
`Curl.Protocol.Abstractions`, and never `Curl.Protocol.Http` or another extension, so the signer
is written here rather than shared with the S3 extension. The options and the catalogue
registration arrive in BL-4125.

- `Signing/GcsRequestSigner` signs a request with an HMAC key as the Cloud Storage "V4 signing
  process" says, in the `Authorization` header form: it returns the `x-goog-date`,
  `x-goog-content-sha256` and `Authorization` (`GOOG4-HMAC-SHA256`) headers to add, at the
  injected `TimeProvider`'s time, with credential scope `date/region/storage/goog4_request`.
- `Signing/GcsCanonicalRequest` builds the canonical request and the signed headers list.
- `Signing/GcsUriEncoder` percent-encodes the canonical URI and query string.
- `Signing/GcsSigningRequest` and `Signing/GcsHmacKey` (region `auto` by default) are the
  signer's inputs.
- `Signing/GcsBearerAuthorization` builds `Authorization: Bearer <token>` from an OAuth 2.0
  access token.
- `Xml/GcsListBucketResultParser` reads the XML API "List objects" `ListBucketResult` body into a
  `GcsObjectListing`: prefix, delimiter, the `Contents` entries (`GcsListedObject`: key, size,
  last write, ETag), common prefixes, truncation and the continuation value
  (`NextContinuationToken`, else `NextMarker`).
- `Xml/GcsErrorResponseParser` reads the "XML API error responses" `Error` body into a
  `GcsErrorResponse` (`Code`, `Message`, `Details`).
- Both match elements by local name and return a `GcsXmlParseResult`, never an exception, with the
  reason when a body is not well-formed or lacks a required element (`Xml/GcsXmlBody`).
- `Http/GcsHttpClientConnection` sends one HTTP/1.1 request at a time over an `IConnection`
  (method, target, `Host`, headers, optional body with `Content-Length`) and reads the status
  line, headers and a body framed by `Content-Length`, chunked or the close (RFC 9112), saying
  whether the connection persists; every failure is a `GcsHttpException` naming a
  `GcsHttpFailure`. TLS comes from `ConnectTarget.UseTls` through the connector. It is the S3
  extension's `Http/` shape, written here because an extension references no other extension.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.GoogleCloudStorage.UnitTests`.
