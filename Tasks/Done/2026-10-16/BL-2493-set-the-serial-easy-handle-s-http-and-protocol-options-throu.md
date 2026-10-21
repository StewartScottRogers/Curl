---
id: BL-2493
title: Set the serial easy handle's HTTP and protocol options through CurlEasy.SetOption
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2451]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2493 — Set the serial easy handle's HTTP and protocol options through CurlEasy.SetOption

## Goal

A serial transfer's EasyHttpOptions and EasyProtocolOptions reach its Libcurl CurlEasy as curl_easy_setopt calls (CurlEasy.SetOption, one per CURLOPT_* the command line sets), as upstream's config2setopts.c sets them, instead of being written whole onto the engine transfer's Options.

## Context

ADR-0506, ADR-0624; BL-2451 routed serial transfers through CurlEasy (ToolCurlEngine, ToolEasyHandle) and sets only CURLOPT_URL through SetOption; CurlCommandRunner.SetUpToolEasy still assigns ToolEasyHandle.Hooks.Options = EasyOptionsFromCommandLine.EasyOptionsOf(...). Turn HttpOf (the -F parts through a CurlMime from the runner's library) and ProtocolOf into SetOption calls for every field libcurl has a CURLOPT_* for, using Libcurl's setters (Libcurl/Options/<Group>) as they are. A field with no CURLOPT_* (a Curl extension such as the --httpsig-* options, or a value a setter would refuse or clamp differently from what the tool needs today) stays on the hooks' Options; record each such field and why in an ADR. A setter Libcurl lacks or that answers differently from the record is filed as a Libcurl task (touches Libcurl/Options/<Group>), not changed here. A non-Ok answer from SetOption must end the transfer as curl's tool does (its setopt failure path), measured on real curl where reachable.

## Acceptance criteria

- [x] Every EasyHttpOptions and EasyProtocolOptions field the command line sets with a CURLOPT_* equivalent is set through CurlEasy.SetOption; CurlCommandRunnerEasyTransferTests pins it for a representative option of each group.
- [x] The output is byte for byte unchanged: dotnet build is clean and the fast tests pass with no test deleted, skipped or weakened, and Curl.Conformance.UnitTests/PassingUpstreamCases.txt is unchanged or grown.
- [x] Measure-CodeQuality.ps1 -Library Curl.Console reports 0 failing members.
- [x] Curl.Console/CLAUDE.md says which fields go through SetOption and which stay on the hooks.

## Notes

- Plan: HTTP and protocol rows in `EasySetoptsFromCommandLine.HttpProtocols.cs`, the same `SetoptRow` model as
  BL-2491/BL-2492, after the proxy rows; one `CURLOPT_COOKIEFILE` per `-b` file (the setter appends one file per call).
  The `-F` form is built by the new `CurlMimeFromFormBody` with `ToolCurlEngine.MimeInit` (`curl_mime_init` on the
  runner's library) and set by `SetUpToolEasy` as `CURLOPT_MIMEPOST` after the groups' calls. Decisions in ADR-0652
  (ADR-0649 was already taken on the shared branch).
- Kept on the hooks: a form with a `@-`/`<-` part (the tool's standard input reader has no `curl_mime_*` call that
  builds the same part) or an encoder `curl_mime_encoder` refuses; `HttpPost`, `StreamDependency`, `CookieList` (the
  command line never sets them); values a setter would refuse or hold (`--max-redirs`/POSTFIELDSIZE below -1,
  non-whole `--expect100-timeout`, stream weight outside 1-256, `--tftp-blksize` outside 8-65464, `--hostpubmd5` not 32
  characters).
- `curl_mime_filedata`'s `CURLE_READ_ERROR` for a missing file is ignored: the part is still set and the transfer
  fails reading it, as today. No Libcurl setter answered differently from the record, so no Libcurl task was filed.
- The setopt failure path is ADR-0631's (`ToolEasyHandle.SetOptions`); no command line the parser accepts reaches a
  refused HTTP or protocol call (the parser already refuses a bad `--hostpubmd5` and `--max-redirs`), so there was
  nothing new to measure on real curl.
- `KeptOnHooks_FieldsWithoutCurlopt_StayOnTheHooksAndTheRestAreLibcurlDefaults` asserted the HTTP group stayed whole,
  which this task changes by design; it now asserts the callbacks group stays whole, and the HTTP/protocol keeping is
  pinned by three new `EasySetoptsFromCommandLineTests` and `CurlMimeFromFormBodyTests`.
- Measured: build clean; fast tests all green (41 projects, Curl.Console.UnitTests 2820 passed, Curl.Conformance.UnitTests
  3130 passed), `PassingUpstreamCases.txt` unchanged; `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 4d57dfb4 84393800 e3fd321b 908e5cf5.
- 2026-10-16: Doing -> Done. Serial transfers set their HTTP and protocol options, and the -F form as CURLOPT_MIMEPOST, through CurlEasy.SetOption
