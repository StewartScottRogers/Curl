# ADR-1260 — s3, gs, az, oci and grpc report status, HTTP version, content type and header size for -w

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4938
- Decided by Claude under Stewart's delegation.

## Context

The outside fitness review (EXT-016) found `-w "%{response_code} %{http_version} %{content_type} %{size_header}"`
printed `000 0  0` for these schemes, because their handlers never filled `TransferResult.Report`.

## Decision

- s3, gs, az and oci: each library's `*HttpHeadReporter` (ADR-1244), which already sees every response head line,
  also remembers the last response's status code, HTTP version (1.0 for an `HTTP/1.0` status line, else 1.1) and
  `Content-Type`, and the summed size of every head line read. The handler gives `reporter.Report` to the result,
  success or failure, so `-w` also reports a 404.
- grpc: `GrpcHttp2Call.ResponseReader` builds the report from the first HEADERS frame: `:status`, HTTP version 2,
  `content-type`, and a header size of the status line `HTTP/2 200 `, each non-pseudo field as `name: value`, and the
  closing empty line, all CRLF-ended, which is how curl sizes an HTTP/2 head. Trailers are not counted.
- The reporter's `None` is a new instance on each use because it now holds state.

These schemes are extensions, so there is no upstream output to match beyond curl's own http variables.
