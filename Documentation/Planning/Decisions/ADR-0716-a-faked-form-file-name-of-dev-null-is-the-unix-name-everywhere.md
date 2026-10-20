# ADR-0716: A faked form file name of %DEV_NULL is the Unix name everywhere

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation
- Task: BL-2830

## Context

Upstream case test173 posts stdin as a form part with a faked file name,
`-F "fileupload=@-;filename=%DEV_NULL;..."`, and its `<verify><protocol>` pins
`Content-Length: 378`, a length counted with `/dev/null` (9 bytes) in the
`Content-Disposition` line. `UpstreamCurlPlatform.Windows.NullDevice` is `NUL`, so on Windows
Curl sent 372 bytes and the case failed, while the gap office measured it `match` on Linux.

`%DEV_NULL` has two uses in curl 8.22.0's cases: test173's file name, which is only text curl
sends, and test3015's `-o%DEV_NULL`, a file curl opens, which needs the real null device (`NUL`
on Windows; `/dev/null` there would create a file).

## Decision

`UpstreamTestDirectoryComposition.Rewrite`, which already rewrites a case before expansion,
turns `filename=%DEV_NULL` and `filename="%DEV_NULL"` into `/dev/null` on every platform.
Every other `%DEV_NULL` stays `UpstreamCurlPlatform.NullDevice`.

## Consequences

- test173 passes on Windows as on Linux and macOS, with the bytes upstream wrote; test3015 still
  opens `NUL` on Windows.
- No product code changes: the difference was in the harness's expansion, not in Curl.
