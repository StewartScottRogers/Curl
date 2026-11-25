---
id: BL-5869
title: Fix fitness finding EXT-119: --resumable-upload silently bypasses --har, --record, -v and --trace-ascii
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5866]
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5869 — Fix fitness finding EXT-119: --resumable-upload silently bypasses --har, --record, -v and --trace-ascii

## Goal

EXT-119 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: --resumable-upload silently bypasses --har, --record, -v and --trace-ascii. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: **Shared mock for EXT-115, EXT-116, EXT-119, EXT-109, EXT-111** (HTTP, port 27020, logs every request line and header to a file): `OPTIONS /tus` → 204 with tus 1.0.0 headers offering `creation`; `POST /tus` → 201 with `Location: /files/1`; `PATCH /files/1` → 204; `/strong` → 300,000 bytes where `byte[i] = (i*13+5) % 256`, with ETag and Range support; `/plain` → a body with `Cache-Control: max-age=60`. `up.txt` is a 6-byte file. The finding, verbatim:


- **Severity:** Low · **Confidence:** Confirmed (Windows twice, Linux once) · **Area:** `--resumable-upload` with diagnostic options.
- **Repro:**
  - `curl -sS --har ru.har --record rurec --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus; ls ru.har rurec`
  - `curl -sv --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus`
  - `curl -s --trace-ascii tr.txt --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus`
- **Expected:** `--extended-help`: har "Records each HTTP request and response"; record-replay "Records HTTP exchanges to a directory". A plain `-T`, `--segments` and oci all produce HAR entries and `-v` lines. The OPTIONS, POST and PATCH exchanges should appear (or a warning should say they cannot).
- **Actual (.NET):** exit 0; `ru.har` and `rurec` are never created; `-v` prints no `>`/`<`/`*` lines; `tr.txt` is 0 bytes; `--log-file` says `transfer 0 ended: exit 0, 0 bytes` although 6 bytes were uploaded.
- **Likely location:** the resumable hook (`ExtensionTransferHookCatalogue.cs` line 29) completes the transfer without calling `next`, so the HAR (line 39) and RecordReplay (line 46) hooks never run, and its client writes no trace.
- **Acceptance check:** the first command creates `ru.har` with 3 entries (OPTIONS, POST, PATCH) and a non-empty `rurec`; `-v` shows `> PATCH /files/1`.

## Acceptance criteria

- [x] the first command creates `ru.har` with 3 entries (OPTIONS, POST, PATCH) and a non-empty `rurec`; `-v` shows `> PATCH /files/1`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

First-pass finding (no code edited): the fix is a transport change, and it needs files BL-5866 (Doing on the shared branch) holds: `ResumableUploadRequestHeaders.cs` and `ExtensionTransferHookCatalogue.cs`. Requeued behind BL-5866.

Cause confirmed: `ResumableUploadTransferHook.ExecuteAsync` sends OPTIONS/POST/PATCH through its own `ResumableUploadSession` (connector + hand-written HTTP/1.1 client) and never calls `next`, so the HAR, RecordReplay, `-v`, `--trace-ascii` and DPoP hooks and handlers never see them. The catalogue order is already right (resumable sits outside Har and RecordReplay), so the catalogue needs no change once requests go through `next`.

Design for the next claim: make `ResumableUploadSession.SendAsync(method, uri, headers, body, offset)` run each request through `next` instead of the connector. Build a private `ITransferContext` wrapper in the library (Upload => null, Url => uri, Http with CustomMethod and Body = the slice read from the seekable body into memory, Headers plus the flow's headers; cross-origin: drop Authorization, Cookie and Proxy-Authorization from `-H`; NoBody for HEAD; PostWithoutData for the bodyless POST; Output via WithOutputs to a MemoryStream). Read `TransferResult.Report` (ResponseCode, ResponseHeaders) for the `ResumableUploadExchange`; map exit 56 and 55 to `canResume: true` and any other failure to `canResume: false` with the result's exit code and message. curl's stack then does auth, TLS, proxy, User-Agent and tracing, so `ResumableUploadRequestHeaders`, the whole `Http/` folder and `ResumableUploadInterruptibleConnection` can go, and the 3-argument (services) `ExecuteAsync` with them. About 28 tests in `ResumableUploadTransferHookTests` use `ScriptedConnector` and must be rewritten against a scripted `next`; add a test that each request reaches `next` (OPTIONS, POST, PATCH). Measure real exchanges against the shared mock after `dotnet build Curl.Console`. This is large (about 15 files deleted, 3 rewritten, tests rewritten, ADR, the library's CLAUDE.md): if the claim finds it past one run, split it before editing.

Check BL-5866 first: with requests through `next`, `--dpop` is applied by its own hook, so BL-5866's header code may be gone or moot.
- 2026-11-25 claim: done as designed (ADR-1377). ResumableUploadSession calls next with a ResumableUploadRequestContext per request; the copied HTTP/1.1 client (Http/), ResumableUploadInterruptibleConnection and ResumableUploadRequestHeaders are deleted, and the hook no longer needs TransferHookServices. Exit codes 52, 55 and 56 resume; any other failure ends the upload. Hook tests rewritten against ScriptedNext. Catalogue unchanged (resumable already sits outside Har and RecordReplay), so ExtensionTransferHookCatalogue.cs was not edited.
- Measured with Curl.Console against a PowerShell HttpListener tus mock on 127.0.0.1:27020: the first command exits 0 and ru.har holds OPTIONS, POST and PATCH entries, rurec holds 3 exchanges; -v shows > OPTIONS /tus, > POST /tus, > PATCH /files/1; --trace-ascii writes 2605 bytes.
- Measure-CodeQuality for Curl.Extension.ResumableUpload.UnitLibrary: 0 failing members. dotnet build clean, fast tests green (platform-neutral: no paths, certificates or OS text in the new tests).


## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 0f6d8102 69116afe 3d836d5e.
- 2026-11-24: Doing -> Backlog. Needs ResumableUploadRequestHeaders.cs and the hook catalogue, held by BL-5866 in Doing; waits for it. No code edited; design in Notes.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 0f6d8102 69116afe 3d836d5e.
- 2026-11-25: Doing -> Done. --resumable-upload sends OPTIONS, creation and PATCH through curl's transfer, so --har, --record, -v and --trace-ascii see them
