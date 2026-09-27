---
id: BL-030
title: Wire UploadUrl into the transfer dispatcher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-010, BL-012, BL-292]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-005
created: 2026-09-26
completed:
---
# BL-030 — Wire `UploadUrl` into the transfer dispatcher

## Goal

Every `-T`/`--upload-file` transfer dispatched from `Curl.Cli.UnitLibrary` has its URL
resolved through `Curl.Cli.UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile`, then
parsed and normalised, before the upload source is opened, with the exit codes upstream
curl 8.21.0 gives.

## Context

BL-012 added the pure resolver `Curl.Cli.UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile`
(and `UploadUrl.IsStandardInput`) in `Curl.Cli.UnitLibrary\UploadUrl.cs`. Nothing calls
it yet: `Curl.Console\Program.cs` still prints `curl: not implemented yet` and returns
`CurlExitCode.FailedInit`, and `Curl.Cli.UnitLibrary` has no option parser or transfer
dispatcher. **No task on the board files the option parser or the dispatcher** (BL-027
also waits on a parser that no task creates). This task cannot start until both exist;
if the runner finds them still missing, move this task to `Blocked` naming that gap
rather than writing the parser here.

Upstream behaviour, from curl 8.21.0 `src/tool_operate.c` (`setup_transfer_upload`) and
`src/tool_operhlp.c` (`add_file_name_to_url`):

- `-T` arguments pair with URLs in order: `-T a -T b URL1 URL2` uploads `a` to URL1 and
  `b` to URL2, each resolved against its own URL (`URL1/` gives `URL1/a`).
- `-T -` and `-T .` (standard input) skip resolution; the URL is used unchanged.
- For every non-stdin `-T`, upstream parses the URL with
  `CURLU_GUESS_SCHEME | CURLU_NON_SUPPORT_SCHEME`, so a malformed URL exits 3
  (`CurlExitCode.UrlMalformat`) at that point, even when nothing is appended, and before
  the upload source is opened: `-T nosuchfile 'http://h/d ir/'` exits 3, not 26. A valid
  URL with a missing file exits 26 (`CurlExitCode.ReadError`).
- The rebuilt URL is normalised: `http://` is guessed for `host/dir/`, the scheme is
  lowercased, and `-T local.txt http:/host` yields `http://host/local.txt`.

The parse-and-normalise step is URL-layer work, which BL-012's notes leave to the URL
representation BL-010 chose: ADR-0010 (Accepted) replaces `System.Uri` with `CurlUrl`,
the curl-compatible URL type BL-292 adds to `Curl.Protocol.Abstractions.UnitLibrary`.
That is why this task depends on BL-010, BL-012 and BL-292. Parse and normalise with
`CurlUrl`; do not introduce a second URL parser here.

Upstream references: https://curl.se/docs/manpage.html#-T and
https://curl.se/libcurl/c/libcurl-errors.html, checked against curl 8.21.0.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests` shows pairwise resolution: `-T a -T b http://h/1/ http://h/2/`
      dispatches `a` to `http://h/1/a` and `b` to `http://h/2/b`, in that order.
- [ ] A test shows `-T - http://h/d/` and `-T . http://h/d/` dispatch to `http://h/d/`
      unchanged.
- [ ] A test shows `-T nosuchfile 'http://h/d ir/'` returns `CurlExitCode.UrlMalformat`
      (3), and that the upload source was never opened (the injected `IFileSystem`
      records no open call).
- [ ] A test shows `-T nosuchfile http://h/d/` returns `CurlExitCode.ReadError` (26).
- [ ] A test shows `-T local.txt http:/host` yields the effective URL
      `http://host/local.txt`, and `-T local.txt host/dir/` yields
      `http://host/dir/local.txt`.
- [ ] The FR-005 row in `Documentation/Product/Requirements.md` no longer lists
      resolving a `/`-ending `-T` URL, or wiring it in, as an open gap.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green; no test needs
      `TestCategory=Integration`.

## Notes

Globbing of the `-T` argument is BL-031 and is out of scope here.

## Log

- 2026-09-26: Created.
- 2026-09-26: Depends on BL-292 as well: ADR-0010 accepted `CurlUrl` as the URL representation (BL-010).
