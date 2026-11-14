---
id: BL-5127
title: Count streamed bytes in the -m timeout message and size_download for coap --coap-observe (EXT-036, coap half)
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs, Curl.Extended.Protocol.Coap.UnitTests/Observe/CoapObservationTests.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams-Coap.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5127 — Count streamed bytes in the -m timeout message and size_download for coap --coap-observe (EXT-036, coap half)

## Goal

A `-m` timeout on a `coap://` or `coaps://` `--coap-observe` stream reports the notification bytes written (`with N bytes received`) and `%{size_download}` equals the bytes on stdout, as it already does for nats and signalr.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-036 (Low, Confirmed, 3 runs each), re-reported as CHANGED: the earlier report's fix (task BL-5044, `Tasks/Done/2026-11-13/BL-5044-count-streamed-bytes-in-the-m-timeout-message-and-size-downl.md`) covered nats and signalr only. The Socket.IO half is a separate task (`Curl.Extended.Protocol.SocketIo`).

**Repro.** Run a CoAP server on 127.0.0.1:27050 with an observable resource `/obs` notifying `v<n>` every 0.5 s (the review used aiocoap 0.4.17; use a PowerShell or C# file-based stand-in, never Python). Run `curl -sS -m 1.6 --coap-observe coap://127.0.0.1:27050/obs -w '|size=%{size_download}\n'`.

**Expected.** `CurlSurface-Transfer-MessagingStreams-Coap.md` (and `CurlSurface-Transfer-Realtime.md`): `%{size_download}` is the bytes written; nats prints `(28) ... with 3 bytes received` and `size=3`, signalr `with 16 bytes received` and `size=16`. coap should report the notification bytes it wrote.

**Actual.** coap prints the `v<n>` lines, then `curl: (28) Operation timed out after ... with 0 bytes received` and `|size=0`.

**Where to start.** `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs`: `WriteAsync(ITransferContext, CoapMessage)` (about line 240) writes `notification.Payload` and `NotificationEnd` to `context.Output` and never calls `context.Progress.ReportDownloaded`. Keep a running byte total across notifications (payload plus the separator written after each; header output via `-D` is not counted, as for the other schemes) and report it after each notification, the pattern in `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs:136-137` and in BL-5044's change. `ITransferContext.Progress` is the reporter. Also check the final `TransferResult` the observation returns when the stream ends other than by timeout (cancellation by `-m`, server cancel, count limit) so its downloaded size matches the bytes written.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.Coap.UnitTests/Observe/CoapObservationTests.cs` (existing file, or a new `CoapObservationProgressTests.cs` beside it) prove that after two notifications with payloads `v1` and `v2` the progress reporter has been told the running total of the bytes written to the output, and that the total never resets between notifications.
- [ ] A test proves that a `-m` timeout after notifications surfaces as `CurlExitCode.OperationTimedOut` (28) with `with N bytes received`, N equal to the bytes on the output, through the observe hook path used by the existing `CoapObserveTransferHookTests`.
- [ ] The existing CoAP Observe tests stay green, including a stream with no notifications (still `0 bytes received`).
- [ ] The `CurlSurface-Transfer-MessagingStreams-Coap.md` row for the timeout message and `%{size_download}` states it for observe, "Covered by" naming a test from this task.
- [ ] The tests are platform-neutral (no real socket).
- [ ] `dotnet build Curl.Extended.Protocol.Coap.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: the coap command prints `|size=N` where N equals the bytes on stdout.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 4a49769f f4ede839 1c76155e 5c2669ad d6cd4299 d3bdf191.
