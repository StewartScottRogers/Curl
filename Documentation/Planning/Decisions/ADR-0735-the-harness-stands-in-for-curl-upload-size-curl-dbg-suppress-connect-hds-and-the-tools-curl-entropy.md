# ADR-0735 — The harness stands in for CURL_UPLOAD_SIZE, CURL_DBG_SUPPRESS_CONNECT_HDS and the curl tool's CURL_ENTROPY WebSocket key

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2910
- Decided by Claude under Stewart's delegation.

## Context

Upstream cases 447, 2349 and 2300 need the Debug feature: each sets a variable curl's debug build
reads and its release build ignores (curl-8_22_0).

- `CURL_UPLOAD_SIZE` (`src/tool_operate.c`): the tool hands libcurl this number as the `-T` file's
  size, so curl sends it as `Content-Length` and stops after that many bytes of a growing file.
- `CURL_DBG_SUPPRESS_CONNECT_HDS` (`lib/sendf.c`, `cw_download_write`): a CONNECT reply head is
  dropped from the client writers exactly as `data->set.suppress_connect_headers`
  (`--suppress-connect-headers`) drops it, on the line beside it.
- `CURL_ENTROPY` with a `ws://` URL through the curl tool: the forced random sequence fixes the
  `Sec-WebSocket-Key`. ADR-0678 gave it only to the libtest engine.

## Decision

The stand-ins are the conformance harness's (`UpstreamConformanceTests.ToolTransferStandIns.cs`);
the shipped `curl` reads none of the three variables.

1. `CurlComposition.CreateRunner` takes two new optional parameters, both off by default:
   `webSocketRandomSource`, passed to the WebSocket handler of every option group, and
   `fileSystem`, the runner's file system in place of `PhysicalFileSystem`.
2. Under `CURL_UPLOAD_SIZE`, `RunCurlAsync` passes `UploadSizeCappingFileSystem`: every file read
   reports that length and ends there (a value that is not a number is 0, as `atoi` gives).
3. Under `CURL_DBG_SUPPRESS_CONNECT_HDS`, `RunCurlAsync` appends `--suppress-connect-headers` to
   the command line, since upstream makes the two the same check. It reaches the last option group
   only; no upstream case sets the variable beside `--next`.
4. Under `CURL_ENTROPY` (or `CURL_WS_FORCE_ZERO_MASK`), `RunCurlAsync` passes the same
   `WebSocketRandomSourceFor` source `CreateEngine` gives the libtest engine.
5. `DebugStandInRules` reports Debug to a case setting `CURL_UPLOAD_SIZE` or
   `CURL_DBG_SUPPRESS_CONNECT_HDS`; the existing `ws` rule already covers 2300.

## Consequences

Cases 447, 2349 and 2300 pass and are on the ratchet. The capping file system caps every read of
the run, not only the `-T` file; only 447 sets the variable, and it reads no other file.
