# Curl.Extension.S3.UnitLibrary

The S3 extension (ADR-0999, ADR-1012). Namespace `Curl.Extension.S3`. It references nothing but the base
class library so far, and never `Curl.Protocol.Http` (whose `--aws-sigv4` code is curl's, not
this extension's). The XML wire format (`Xml/`, BL-4198), the client flow (`Http/`, BL-4199) and
the options arrive in later tasks.

- `Signing/S3RequestSigner` signs a request with AWS Signature Version 4 as the Amazon S3 API
  reference "Authenticating Requests (AWS Signature Version 4)" says, for a payload in a single
  chunk (or `UNSIGNED-PAYLOAD`): it returns the `x-amz-date`, `x-amz-content-sha256` and
  `Authorization` headers to add, at the injected `TimeProvider`'s time.
- `Signing/S3CanonicalRequest` builds the canonical request and the signed headers list.
- `Signing/S3UriEncoder` percent-encodes as the reference's "UriEncode" says; a path is encoded
  one segment at a time, once (S3 does not double-encode).
- `Signing/S3SigningRequest` and `Signing/S3Credentials` are the signer's inputs.

Tested against the specification, not against curl: the signer reproduces the reference's four
single-chunk examples. Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.S3.UnitTests`.
