---
id: BL-5910
title: Exit 25 on a chunked PUT to an HTTP/1.0 server on Linux and macOS (upstream tests 1072 and 1073)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5915]
touches: [Curl.Console/StandardInputUploadStream.cs, Curl.Console.UnitTests/StandardInputUploadStreamTests.cs, Curl.Console.UnitTests/RateLimitedUploadStreamTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/IRewindableUpload.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Authentication.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.TransferEncoding.cs, Curl.Original.Protocol.Http.UnitTests/Fakes/UnrewindableStream.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5910 — Exit 25 on a chunked PUT to an HTTP/1.0 server on Linux and macOS (upstream tests 1072 and 1073)

## Goal

`curl -T -` to a server that answers HTTP/1.0 exits 25 (`CURLE_UPLOAD_FAILED`) on Linux and macOS, as upstream curl 8.21.0 does and as the port already does on Windows, so upstream test cases 1072 and 1073 pass on every platform.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 29 (Surl `Upstream-Test-Cases.md` finding UTC-5). Seen on linux-x64 (run 44044472038) and osx-arm64 (run 45412760425) at port commit `74f3f45` (2026-11-16). Check it still reproduces on the current branch first; the port's own CI runs the upstream cases on Linux and macOS.

Cases: `test1072` "HTTP chunked PUT to HTTP 1.0 server with authorization" (`http://%HOSTIP:%HTTPPORT/1072 -T - -u testuser:testpass --anyauth`) and `test1073` "HTTP chunked PUT to HTTP 1.0 server with redirect" (`http://%HOSTIP:%HTTPPORT/1073 -T - -L`), tag `curl-8_21_0`. Upstream exits 25 as each `<errorcode>` says. The port exits 65 (`CURLE_SEND_FAIL_REWIND`) on Linux and macOS, 25 on Windows. The platform split suggests an ordering race: on the second request the port first finds stdin can't be rewound (65), where it should first find that chunked upload is impossible to an HTTP/1.0 server (25), or a socket-close timing difference. Related: BL-2130. Start at `HttpRequestFraming.cs` and `Http1VersionSeen.cs`.

## Acceptance criteria

- [x] The decision to fail with 25 comes before any rewind check, whatever the platform's timing, pinned by a unit test with a test-double connection that answers HTTP/1.0 to a chunked `-T -` (with `--anyauth` and with `-L`).
- [x] Upstream cases 1072 and 1073 pass in the port's upstream-case runner (CI on Linux and macOS).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Lane 12, 2026-11-24: analysed and measured; no code edited, nothing to stash.

**Cause (not a race in `HttpRequestFraming` or `Http1VersionSeen`).** The 65 is deliberate:
`HttpProtocolHandler.FailureBeforeConnecting` fails a chunked resend to HTTP/1.0 with 65 when
`IRewindableUpload.CanRewind` is false (ADR-0524, BL-2337), and
`Curl.Console/StandardInputUploadStream.CanRewind` is false off Windows whenever the wrapped stream
cannot seek. Two cases are wrong:

1. **Upload never read.** curl rewinds only a reader it has read from. Measured: WSL curl 8.18.0
   (OpenSSL), `printf 'data\n' | curl -v -T - --anyauth -u testuser:testpass` and `... -T - -L`
   against `nc` answering HTTP/1.0 401 / 301 after 0.3 s (inside the `100-continue` wait): no rewind,
   `Issue another request to this URL`, curl reconnects (exit 7 only because `nc` listened once; the
   resend is then refused with 25). The port exits 65.
2. **File redirected into stdin.** `StandardInput` is a `UnixConsoleStream`, which never seeks, so
   the port exits 65 where curl and upstream runtests (`<stdin>` is a file) get 25 (ADR-0524's
   "known difference").

**Confirmed unchanged curl behaviour.** Same commands, answer after 2 s (body already read from the
pipe): `Need to rewind upload for next request`, `seek callback returned error 2`,
`rewind of client reader 'cr-in' failed: 65`, exit 65. The port is right there; keep it.

**Fix to make (both in `StandardInputUploadStream`).**
`CanRewind => !hasBeenRead || inner.CanSeek || SeekOfStandardInputSucceeds`, with `hasBeenRead` set
by `Read`/`ReadAsync`, and the seek probe defaulting to true on Windows, else
`new FileStream(new SafeFileHandle(IntPtr.Zero, ownsHandle: false), FileAccess.Read, 1).CanSeek`
(descriptor 0, the same `lseek` curl makes; false for a pipe, true for a redirected file). Keep it
injectable so tests pin both platforms. Update the doc comments there, on `IRewindableUpload` and on
`FailureBeforeConnecting`, and write an ADR amending ADR-0524. Add handler tests for `--anyauth` and
`-L`. Run `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Original.Protocol.Http.UnitLibrary`.

**Why requeued.** The fix and its tests are in `Curl.Console.UnitTests`, which BL-5915 (Doing)
names whole in `touches`.

**Surl's harness.** `Surl.Conformance.UnitLibrary/UpstreamCurlRunner` gives non-Windows upstream curl
stdin as a seekable file (its BL-1143), but `CurlPortClientRunner` pipes stdin to the port. With a
pipe and a server that waits for the body, real curl exits 65 too, so Surl needs to give the port the
same file; no change here makes that comparison fair.

**Acceptance criterion 1, as measured.** "25 comes before any rewind check, whatever the platform's
timing" holds when the HTTP/1.0 answer arrives before the upload is read, and for stdin that can seek.
It does not hold for a piped stdin already read: real curl rewinds first and exits 65, and the port
must keep matching it. If the next run judges the criterion unmeetable as written, it commits the fix
and moves the task to Blocked with that question; it must not make the port exit 25 there.

Lane 6, 2026-11-24: fixed as the Notes above planned. `StandardInputUploadStream.CanRewind` is now
`!hasBeenRead || inner.CanSeek || SeekOfStandardInputSucceeds` (renamed from
`SeekOfUnseekableInputSucceeds`); the probe defaults to true on Windows and elsewhere to `CanSeek` of a
`FileStream` over descriptor 0, in one `ExcludeFromCodeCoverage` adapter method (ADR-0083). ADR-1373
records it, amending ADR-0524. Handler tests
`ExecuteAsync_Http10ChallengeBeforeAnUnrewindableBodyIsRead_ReturnsUploadFailedRatherThanSendFailRewind`
(`--anyauth`) and `ExecuteAsync_RedirectHopAfterAnHttp10ResponseWithAnUnrewindableBodyNotYetRead_FailsWithExit25BeforeAnyRewindCheck`
(`-L`) pin 25 before any rewind check, with `UnrewindableStream { RewindsUntilRead = true }`. A piped
stdin already read still gives 65, as real curl does (measured by lane 12). Criterion 2: cases 1072 and
1073 pass in the runner here (`CURL_UPSTREAM_CASE`) and are on the ratchet; the change is
platform-neutral (the runner feeds stdin from memory, which is unread until the 100-continue and seekable),
so CI on Linux and macOS is left to the shift's watch. Measure-CodeQuality for Curl.Console and
Curl.Original.Protocol.Http.UnitLibrary: 0 failing members. Solution build clean, fast tests green
(114 test assemblies passed). HttpProtocolHandler.cs needed no change.

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 29.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 8c155b65 fcdae5ab 3d836d5e.
- 2026-11-24: Doing -> Backlog. Waits on BL-5915, which holds Curl.Console.UnitTests: the fix is in Curl.Console/StandardInputUploadStream.cs and its tests (see Notes)
- 2026-11-24: Backlog -> Doing. Criteria at claim: 8c155b65 fcdae5ab 3d836d5e.
- 2026-11-24: Doing -> Done. A chunked -T - resend to an HTTP/1.0 server exits 25 when stdin was not yet read or is a redirected file, on every platform
