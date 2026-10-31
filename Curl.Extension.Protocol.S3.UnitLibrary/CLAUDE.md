# Curl.Extension.Protocol.S3.UnitLibrary

The S3 extension (ADR-0999, ADR-1013, ADR-1018, ADR-1090). Namespace `Curl.Extension.Protocol.S3`. It references the
base class library, `Curl.Protocol.Abstractions` and `Curl.Extension.Abstractions`, and never `Curl.Protocol.Http`
(whose `--aws-sigv4` code is curl's, not this extension's). It adds no options.

- `Client/S3Extension` gives the description and handler factory, registered in `Curl.Extension.Composition`.
- `Client/S3ProtocolHandler` serves `s3://bucket/key`: `-T` uploads, an empty key or one ending in `/` lists, otherwise
  it downloads. `Client/S3Configuration` reads `--user` and the `AWS_*` variables; `Client/S3Endpoint` picks the
  virtual-hosted or path-style server; `Client/S3Client` signs and sends one request per connection and turns a status
  of 300 or more into exit 22; `Client/S3Operations` holds GetObject, PutObject, the multipart upload and ListObjectsV2.

- `Xml/S3ListBucketResultParser`, `Xml/S3InitiateMultipartUploadResultParser` and
  `Xml/S3ErrorResponseParser` read the ListObjectsV2, CreateMultipartUpload and error response
  bodies with `System.Xml.Linq` (no `XmlSerializer`, so AOT-safe), matching elements by local name
  so a body in the S3 namespace and one without read alike. Each returns an `S3XmlParseResult<T>`:
  a body that is not well-formed, has another root or lacks a required element is a failure with
  its reason, never an exception. `Xml/S3XmlBody` holds the shared reading.
- `Xml/S3CompleteMultipartUploadWriter` writes the CompleteMultipartUpload request body, parts in
  ascending part number, in the S3 namespace, with no declaration or indentation.

- `Http/S3HttpClientConnection` sends one HTTP/1.1 request at a time over an `IConnection` (RFC 9112)
  and returns an `S3HttpResponse` whose body is a stream framed by `Content-Length`, chunked or the
  close; `ConnectionPersists` says whether the caller may mark the connection reusable once the body
  is read. Every failure is an `S3HttpException` naming an `S3HttpFailure`. `Expect: 100-continue`
  is not sent (ADR-1018).

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
`Curl.Extension.Protocol.S3.UnitTests`.
