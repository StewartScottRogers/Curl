---
id: BL-5901
title: Fix fitness finding EXT-126: the conformance docs say "-d wins when both are given" with -T, but the CLI refuses -d with -T (exit 2) for grpc and kafka
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineWarning.cs, Documentation/Conformance/Extended]
requirement: none
created: 2026-11-24
completed:
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

- [ ] `curl -sS -d dwins -T somefile kafka://localhost:38092/dw/0` exits 0 and consuming `kafka://localhost:38092/dw/0` prints `dwins`; or the five docs no longer claim `-d` wins.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 393602a3 69116afe 3d836d5e.
