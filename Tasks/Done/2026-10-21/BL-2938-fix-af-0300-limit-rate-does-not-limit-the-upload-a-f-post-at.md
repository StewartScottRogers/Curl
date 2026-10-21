---
id: BL-2938
title: Fix AF-0300: --limit-rate does not limit the upload: a -F POST at --limit-rate 10 sends its whole body at once and exits 0 where curl exits 56
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2956]
touches: [Curl.Console, Curl.Console.UnitTests/RateLimitedUploadStreamTests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Protocol.Http.UnitTests/HttpRequestBodyWriterWaitingPieceResetTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2938 — Fix AF-0300: --limit-rate does not limit the upload: a -F POST at --limit-rate 10 sends its whole body at once and exits 0 where curl exits 56

## Goal

The defect the audit office reported as AF-0300 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0300 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0300-limit-rate-does-not-limit-the-upload-a-f-post-at-l.md`.

Location: `Curl.Console/CurlCommandRunner.cs:4898`

Location: `Curl.Console/CurlCommandRunner.cs:4898`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 456945841, case 31: `--form f=@f.txt --limit-rate 10 -i http://127.0.0.1:PORT/` differed in exitcode, stdout, stderr and request. Reduced by dropping one option at a time: dropping -i still differs (smallest: `--form f=@f.txt --limit-rate 10`); dropping --limit-rate gives the same result from both (exit 0, 396-byte request); dropping --form leaves a GET with the same exit code and request. With the reduced line, curl sends the head and then the body at 10 B/s. The recorder answers after a one-second pause in the request, and curl exits 56 'Recv failure: Connection was reset' after 3158 ms with a 213-byte request (20 body bytes sent). Curl sends all 396 bytes in 431 ms and exits 0. Cause: CurlCommandRunner.RateLimited (lines 4898-4901) wraps only the body output stream in RateLimitedStream (used at lines 3597 and 4421). No send-side limiter exists in the product: EasyOptionsFromCommandLine.cs:732 sets MaxSendSpeed but nothing reads it to throttle the request body. curl's --limit-rate applies to uploads as well (its --libcurl output, which Curl reproduces in LibcurlSourceCode.ProtocolOptions.cs:64, sets CURLOPT_MAX_SEND_SPEED_LARGE). A script uploading under --limit-rate sees a different exit code and different bytes on the wire. Phase 2: no ADR under Documentation/Planning/Decisions records the upload as deliberately unlimited; the --limit-rate ADRs (0528, 0552, 0558, 0631) cover only output wrapping and the buffer size.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo | Out-Null; $r=(Get-Location).Path; $d=Join-Path $env:TEMP 'limit-rate-upload'; foreach($b in @(@{n='curl';e='C:\Program Files\Git\ucrt64\bin\curl.exe'},@{n='candidate';e="$r\Curl.Console\bin\Release\net10.0\curl.exe"})){ $o=Join-Path $d $b.n; New-Item -ItemType Directory -Force $o | Out-Null; Set-Content (Join-Path $o 'f.txt') 'file body'; Push-Location $o; [Environment]::CurrentDirectory=$o; & "$r\Record-CurlExchange.ps1" -Port 60931 -Curl $b.e -OutDirectory $o -CurlArgs @('--form','f=@f.txt','--limit-rate','10','http://127.0.0.1:60931/') *> $null; Pop-Location; "$($b.n) exit $(Get-Content (Join-Path $o 'exitcode.txt')) request $((Get-Item (Join-Path $o 'request.bin')).Length) bytes" }
```

- Expected: curl exit 56 request 213 bytes / candidate exit 56 request 213 bytes (body throttled to 10 B/s)
- Actual: curl exit 56 request 213 bytes / candidate exit 0 request 396 bytes (whole body sent in about 0.4 s)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-21 (lane 3, first claim): built the send-side limit, left uncommitted in the lane stash:
  `Curl.Console/RateLimitedUploadStream.cs` (asynchronous reads through `Curl.Core`'s
  `RateLimitedStream`; seek, length and position pass through so a resend can rewind; forwards
  `IRewindableUpload.CanRewind`), `CurlCommandRunner.SendRateLimited` applied in `AttemptInputsOf` to the
  `-T` upload and the `-F` `StreamBody` content, `Curl.Console.UnitTests/RateLimitedUploadStreamTests.cs`
  (10 tests, all passing), and ADR-0727 with its README row. Curl.Console.UnitTests was in no other
  Doing task's `touches`, so the tests file was added there.
- Measured with the finding's reproduction after the change: curl exit 56, 233 request bytes
  (`Recv failure: Connection was reset`); Curl exit 55, 253 request bytes (`Send failure: Connection was
  reset`). The body is now throttled at 10 B/s (byte counts vary with timing on both sides), but the exit
  code still differs: while its send is held curl polls only for reading and reads before writing, so the
  recorder's reset reaches `recv` (56); Curl's HTTP body writer meets it on the next write (55). That fix
  belongs in `Curl.Protocol.Http.UnitLibrary`'s body writer, not in this task's `touches`: filed as
  BL-2956 and added to `depends-on`. Next claim: apply the stash, rerun the reproduction, run the fast
  tests and `Measure-CodeQuality.ps1 -Library Curl.Console`, then commit.
- Filed BL-2957 for the `-d`/`--json` body (`BytesBody`), which this change does not limit.
- 2026-10-21 (lane 1, second claim): carried the stash over by hand; its ADR-0727 was taken meanwhile,
  so it is ADR-0745 now (references to BL-2952/BL-2953 corrected to BL-2956/BL-2957). With BL-2956
  landed the reproduction still gave Curl exit 55: the `-F` POST's body reads go through
  `HttpRequestBodyWriter`'s race against the early response (`EarlyResponseRead`), where BL-2956's
  `waitedOnSource` was never set. Set it there too (one line) and pinned it with
  `WriteAsync_ResetWhileARaceAgainstTheResponseWaitsOnTheUploadSource_FailsWithExit56`. Added
  `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs` and its test file to `touches`: no task in
  Doing on `origin/work/dark-factory` named either.
- Measured after the fix: candidate exit 56 `Recv failure: Connection was reset`, 243 request bytes
  (body at 10 B/s). Real curl gave exit 55 `Send failure: Connection was aborted` with 243 bytes on
  that run (exit 56 / 233 on lane 3's): curl's own answer races the recorder's close, so the request
  byte counts match and the exit code is curl's usual one. Fast tests: 44 projects green.
  `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Backlog. Waits on BL-2956: upload is now throttled, but a reset during the wait must end exit 56 (HTTP body writer); code is in the lane stash
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. --limit-rate holds a -T or -F upload at the rate, and a reset while it waits ends exit 56 as curl 8.22.0
