# ADR-0500: The conformance harness reports headers-api on every platform

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2264
- Decided by Claude under Stewart's delegation

## Context

`headers-api` is the libcurl feature behind `curl_easy_header()`, which `runtests.pl` reports
unless the build disables it (`CURL_DISABLE_HEADERS_API`). The curl tool uses that API to write
`-w %header{name}` and `%{header_json}`; a build without it cannot write either. Upstream cases
that need the variables name `headers-api` in `<features>`: 764, 765, 1670, 1671, 2403 and 2503 on
the command line, and the libtest cases 1940 to 1947.

`UpstreamCurlPlatform` left `headers-api` off with the other libcurl API features, so all of
these skipped. Curl writes both variables (`Curl.Output.UnitLibrary`), so the feature is one Curl
has, and both builds Curl matches (Schannel on Windows, OpenSSL elsewhere) have it.

## Decision

- `UpstreamCurlPlatform` reports `headers-api` on every platform, beside the tool features a
  curl build has unless it disables them.
- The libtest cases that name it still skip for their `<tool>`.

## Consequences

- Measured on Windows: 1670 and 1671 pass and are listed. 2403 (HTTP/2 over nghttpx's TLS front)
  passes when emulating the Linux build and is listed; on Windows it is `SkippedOnThisPlatform`
  for `http/2` (ADR-0488, ADR-0495).
- 764 and 765 run and differ (`%header{name:all:sep}`, BL-2267); 2503 runs off Windows and differs
  (no header block before the JSON over HTTP/3, BL-2268). Unlisted, they are Inconclusive.
