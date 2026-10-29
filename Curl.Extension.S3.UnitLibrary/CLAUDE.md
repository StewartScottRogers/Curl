# Curl.Extension.S3.UnitLibrary

The S3 extension (ADR-0999, ADR-1013). Namespace `Curl.Extension.S3`. It references nothing but the base
class library so far, and never `Curl.Protocol.Http` (whose `--aws-sigv4` code is curl's, not
this extension's). The client flow (`Http/`, BL-4199) and the options arrive in later tasks.

- `Xml/S3ListBucketResultParser`, `Xml/S3InitiateMultipartUploadResultParser` and
  `Xml/S3ErrorResponseParser` read the ListObjectsV2, CreateMultipartUpload and error response
  bodies with `System.Xml.Linq` (no `XmlSerializer`, so AOT-safe), matching elements by local name
  so a body in the S3 namespace and one without read alike. Each returns an `S3XmlParseResult<T>`:
  a body that is not well-formed, has another root or lacks a required element is a failure with
  its reason, never an exception. `Xml/S3XmlBody` holds the shared reading.
- `Xml/S3CompleteMultipartUploadWriter` writes the CompleteMultipartUpload request body, parts in
  ascending part number, in the S3 namespace, with no declaration or indentation.

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
