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

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.GoogleCloudStorage.UnitTests`.
