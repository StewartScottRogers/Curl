---
id: BL-2417
title: Libcurl: seek callback wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2416, BL-2463]
touches: [Libcurl/Callbacks/Seek, Libcurl.UnitTests/Callbacks/Seek, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2417 — Libcurl: seek callback wired through the engine

## Goal

CURLOPT_SEEKFUNCTION is called by the engine when an upload must rewind (redirect, auth retry), with `SeekResult` outcomes as upstream.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Engine tests cover rewind, cannot-seek and fail-seek.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- 2026-10-15 (lane 3, before any code): the engine cannot wire the seek callback on its own. Every rewind today
  is `Stream.Position = start`, taken only when `CanSeek` is true (`RedirectFollower.SeekableStart`/`Rewind` in
  `Curl.Core`; `HttpRequestBodyWriter.Rewound`, `HttpProtocolHandler.TryRewindUpload`/`RewindForResend` in
  `Curl.Protocol.Http`). Making `CallbackReadStream.CanSeek` true would break about a dozen handlers that read
  `Length - Position` as the upload size (`HttpUploadResume.Of`, FTP, SMTP, SCP, SFTP, `file`), and a read
  callback's size is unknown. Filed BL-2463 for a rewind seam in `Curl.Protocol.Abstractions` that `Curl.Core`
  and the HTTP handler call; requeued behind it.
- Plan for the next claim, once BL-2463 is Done: `CallbackReadStream` takes the optional `SeekCallback`, counts
  the bytes read since the last rewind, implements BL-2463's interface by calling `seek(0, SeekOrigin.Begin)`
  only when bytes were read (upstream's `read_len` check), and turns any non-zero `SeekResult` (`Fail` 1,
  `CantSeek` 2) into `seek callback returned error <n>` with `CurlCode.SendFailRewind`, as `cr_in_rewind`
  does in curl 8.22.0 (`CantSeek`'s read-and-discard workaround applies only to `CURLOPT_RESUME_FROM`'s
  forward seek, `cr_in_resume_from`). `ManagedEasyTransfer.UploadOf` passes `options.Callbacks.Seek`. Engine
  tests in `ManagedEasyTransferTests.Callbacks.cs`: a `Continue` + 307 then `Continue` + 200 script under
  `FollowLocation` with a seek that returns `Ok` (upload sent twice, one `seek(0, Begin)`), `CantSeek`
  (exit 65, error 2) and `Fail` (exit 65, error 1). Libcurl: `Libcurl/Callbacks/Seek` adds
  `EasyOptionSetters.Seek.cs` and `EasySeekCallbackSetters` like `Callbacks/Progress` (`SEEKFUNCTION` takes a
  `SeekCallback` or null, else `BadFunctionArgument`; `SEEKDATA` takes any value and changes nothing).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Waits on BL-2463: the engine needs a rewind seam in Curl.Protocol.Abstractions, Curl.Core and the HTTP handler before a read callback can be rewound
- 2026-10-16: Backlog -> Doing.
