---
id: BL-5901
title: Fix fitness finding EXT-126: the conformance docs say "-d wins when both are given" with -T, but the CLI refuses -d with -T (exit 2) for grpc and kafka
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineWarning.cs, Documentation/Conformance/Extended, Curl.Cli.UnitTests/CommandLineUploadRequestMethodTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5901 — Fix fitness finding EXT-126: the conformance docs say "-d wins when both are given" with -T, but the CLI refuses -d with -T (exit 2) for grpc and kafka

## Goal

EXT-126 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: the conformance docs say "-d wins when both are given" with -T, but the CLI refuses -d with -T (exit 2) for grpc and kafka. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low (docs do not match behavior) · **Confidence:** Confirmed (2 runs each) · **Area:** grpc, kafka (the same sentence is in the stomp, signalr and webtransport docs). Windows build.
- **Repro:**
  - `curl -S -d dwins -T somefile kafka://localhost:38092/dw/0 -w "[%{exitcode}]\n"` (any Kafka broker)
  - `curl -S --grpc-proto demo.proto -d '{"name":"x"}' -T req.json grpc://127.0.0.1:38302/demo.Greeter/Echo` (any gRPC server implementing that method)
- **Expected:** `Documentation/Conformance/Extended/Kafka.md` ("Upload with -T"), `Grpc.md` row Grpc:Http2-Request-16 ("-d wins when both are given (BL-5018)"), `Stomp.md` Send-3, `SignalR.md` and `WebTransport.md` all say the `-d` data is sent; exit 0.
- **Actual (.NET):** `Warning: You can only select one HTTP request method! You asked for both PUT (-T, --upload-file) and POST (-d, --data).`, exit 2, nothing sent. Under `-s` it exits 2 with no message.
- **Likely location:** the command-line request-method conflict check, which applies the HTTP rule to every scheme.
- **Acceptance check:** `curl -sS -d dwins -T somefile kafka://localhost:38092/dw/0` exits 0 and consuming `kafka://localhost:38092/dw/0` prints `dwins`; or the five docs no longer claim `-d` wins.

## Acceptance criteria

- [x] `curl -sS -d dwins -T somefile kafka://localhost:38092/dw/0` exits 0 and consuming `kafka://localhost:38092/dw/0` prints `dwins`; or the five docs no longer claim `-d` wins.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-1369): the docs were wrong, not the CLI. Real curl 8.22.0 (Schannel, Windows,
  2026-11-24) refuses `-d` with `-T` with exit 2 for `kafka://` and `mqtt://` too (silent under
  `-s`), and an extension never changes curl's behaviour. So the second branch of criterion 1.
- Corrected the five documents the finding names (Kafka, Grpc, Stomp, SignalR, WebTransport) and
  the others with the same claim (Amqp0, Mqtt5, Nats, Redis, SocketIo, Xmpp). A Grep of
  `Documentation/Conformance/Extended` for `wins`, `when both` and `-d` absent/gave now finds only
  Plugin.md's unrelated search-directory row.
- Handler code and its tests are unchanged: they order `-d` before the upload for a request that
  carries both, which the command line never builds.
- Test: `PutRequestedWith_DataAndUploadToAnExtendedScheme_IsRefusedAsForHttp` in
  `Curl.Cli.UnitTests`; added that file to `touches` (no other task in Doing names it).
  `CommandLineWarning.cs` needed no change, and no production code changed, so no quality run.
- 2026-11-24: `dotnet build` 0 warnings, 0 errors; fast tests 57991 passed, 0 failed, 353 skipped.
  The test is platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 393602a3 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Extended conformance docs and ADR-1369 now say the command line refuses -d with -T (exit 2), as curl does; pinned by a Curl.Cli unit test
