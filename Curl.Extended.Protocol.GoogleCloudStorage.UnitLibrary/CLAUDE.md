# Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary

The Google Cloud Storage extension (ADR-0999), for `gs://` URLs over the Cloud Storage XML API.
Namespace `Curl.Extended.Protocol.GoogleCloudStorage`. It references the base class library and
`Curl.Protocol.Abstractions`, and never `Curl.Original.Protocol.Http` or another extension, so the signer
is written here rather than shared with the S3 extension. It is registered in
`Curl.Extension.Composition` by `Client/GcsExtension` (ADR-1099).

- `Client/GcsProtocolHandler` serves `gs://bucket/object`: `-T` uploads (one PUT Object), an empty
  name or one ending in `/` lists (List objects, `list-type=2`, delimiter `/`, every page), anything
  else downloads (GET Object). `--oauth2-bearer` sends a bearer token, else `--user ACCESS_ID:SECRET`
  signs with an HMAC key, else the request is unauthenticated.
- `Client/GcsEndpoint` is `https://storage.googleapis.com`, path-style, or the emulator
  `STORAGE_EMULATOR_HOST` names. `Client/GcsOperations` holds the three operations,
  `Client/GcsClient` sends one request per connection and turns a status of 300 or more into exit 22
  (`GcsTransferException`).

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
`CLAUDE.md`. Its tests are in `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests`.
