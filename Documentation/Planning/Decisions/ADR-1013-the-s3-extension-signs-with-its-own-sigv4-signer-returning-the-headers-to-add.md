# ADR-1013: The S3 extension signs with its own SigV4 signer, which returns the headers to add

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4197)

## Context

The S3 extension (BL-4124, split into BL-4197, BL-4198 and BL-4199) must sign its requests with AWS
Signature Version 4. `Curl.Original.Protocol.Http` already signs for `--aws-sigv4`, but an extension may not
reference a protocol library (ADR-0999), and that code follows curl's behaviour, while the extension
is tested against the Amazon S3 API reference "Authenticating Requests (AWS Signature Version 4)".

## Decision

- `Curl.Extension.Protocol.S3.UnitLibrary` holds its own signer in `Signing/`, base class library only
  (`SHA256`, `HMACSHA256`). `S3RequestSigner.Sign` takes an `S3SigningRequest` (method, unencoded path,
  query parameters, headers including `host`, payload hash or `UNSIGNED-PAYLOAD`) and `S3Credentials`
  (access key ID, secret key, region), reads the time from an injected `TimeProvider`, and returns the
  `x-amz-date`, `x-amz-content-sha256` and `Authorization` headers rather than changing a request, so
  the client flow (BL-4199) decides how they reach the wire.
- The service is always `s3`. The canonical URI encodes each path segment once; the canonical query
  sorts by encoded name, then encoded value; headers are lower-cased and sorted, values trimmed with
  runs of spaces collapsed, and a header given twice joins its values with a comma.
- Spaces are collapsed by split and join rather than a generated `Regex`, whose generated code the
  quality gates would measure as this library's.

## Consequences

The signer reproduces the reference's four single-chunk examples. Chunked (`STREAMING-...`) payload
signing and presigned URLs are not built; a later task adds them if the client flow needs them.
