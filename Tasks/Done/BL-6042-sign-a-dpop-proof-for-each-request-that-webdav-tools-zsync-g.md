---
id: BL-6042
title: Sign a DPoP proof for each request that --webdav tools, --zsync, --grpc-web and later transfer hooks send
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitTests/DpopWithRequestChangingHooksTests.cs, Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs, Curl.Extension.Dpop.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6042 — Sign a DPoP proof for each request that --webdav tools, --zsync, --grpc-web and later transfer hooks send

## Goal

With `--dpop`, every request an inner transfer hook sends or rewrites carries a proof for that request's own method and URL.

## Context

Found in BL-6029 (ADR-1426), which moved the `--dpop` hook inside `--resumable-upload`'s. In
`ExtensionTransferHookCatalogue` the hooks listed after the DPoP hook can still send several requests, or change the
method or URL, under the one proof the DPoP hook made: start with `WebDavToolsExtension` (calls `next` with
`WithRequest(url, "GET", null)`), `DeltaDownloadExtension`, `GrpcWebExtension`, `GrpcExtension`, `PrivacyPassOptions`
and `CompressionDictionaryExtension`. `ObliviousHttpExtension` sends to a relay, so decide which request its proof
names. RFC 9449 §4.2: `htm` and `htu` match each request and `jti` is unique. Also check that a `DPoP-Nonce` retry of
a request whose body is `Http.Body` (`-F`, a streamed `-d`) sends the whole body again: the hook rewinds only the
`-T` upload.

## Acceptance criteria

- [x] Tests in `Curl.Extension.Composition.UnitTests` show that each request sent by each hook listed above, run with `--dpop`, carries a proof whose `htm` and `htu` are that request's and whose `jti` is unique, or an ADR records why that hook's request differs.
- [x] A test in `Curl.Extension.Dpop.UnitTests` shows a `DPoP-Nonce` retry sends an `Http.Body` request body whole.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: list the DPoP hook after `RecordReplay`'s and before `DnsQuery`'s (ADR-1431). The DNS query and gRPC hooks must
  stay after it, because their handlers recognise the context they wrap by its type; `--ohttp-relay` stays after it, so
  the proof names the target and travels inside the encapsulated request (a test shows the relay and key fetch carry none).
- A `grpc`/`grpcs` proof says `POST` and `http`/`https`. A nonce retry rewinds a `StreamBody` as well as `-T`.
- Done directly, without sub-agents, to stay inside the run's cost cap.
- `touches` gained `Curl.Extension.Dpop.UnitLibrary/CLAUDE.md`, whose description of the hook changed; no other task in
  Doing names it.
- Quality: `Measure-CodeQuality.ps1 -Library` of the two libraries reaches over 45 test projects and passed the
  45 minute command limit, so it was stopped after both libraries' own test projects had reported and scored with
  `-SkipTestRun`: Composition and Dpop 100% line, 100% branch, complexity at most 10, 0 failing members.
- `dotnet build` clean (0 warnings); fast tests 58485 passed, 0 failed, 363 skipped.
- Follow-up filed: BL-6043 (gRPC server reflection requests share the call's proof).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 3707fcbc e2022751 3d836d5e.
- 2026-11-26: Doing -> Done. With --dpop every request the later hooks send or rewrite carries its own proof, and a nonce retry resends a streamed body whole
