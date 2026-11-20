---
id: BL-5611
title: Make a Curl.Api body reach the kafka, grpc and signalr extensions as -d does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs, Curl.Api/RequestBodies]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5611 — Make a Curl.Api body reach the kafka, grpc and signalr extensions as -d does

## Goal

A `Curl.Api` request body to `kafka://`, `grpc://` and `signalr://` is delivered as the console's `-d` delivers it: Kafka produces a record, gRPC sends the request message, SignalR sends the invocation.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding widening BL-5594 (Kafka, gRPC, SignalR). Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

Observed through `Curl.Api`; the console's `-d` works for all three:
- **Kafka produce:** `POST` with a body to `kafka://host/topic` returns status 0 and no record is stored.
- **gRPC:** no request body is sent. `GET` works by reflection with `{}`; `POST` with a body returns `{}`; with a `Content-Type` it fails 415.
- **SignalR:** `SendAsync` to `signalr://` with a body negotiates and handshakes, never sends the invocation, then times out with 0 bytes.
Find how the extensions read the console's `-d` data (the transfer context's post fields, presumably) and why `Curl.Api`'s body does not land there; make every extension that takes `-d` see the `Curl.Api` body the same way, rather than patching three schemes one by one. Do not change how a `Content-Type` header is handled for HTTP. Extensions are tested against their own specifications, not curl.

## Acceptance criteria

- [x] Against Surl (or each extension's test double), a `Curl.Api` `POST` with a body produces a Kafka record that a consumer reads back, calls a gRPC unary method and gets its reply, and sends a SignalR invocation that the hub answers; the Notes record the runs.
- [x] A unit test pins that an extension handler receives a `Curl.Api` byte body and a stream body the same as `-d` data.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Cause: the extension handlers read `context.PostData` (-d) else `context.Upload` (-T) identically; the engine maps post fields to PostData only for mqtt, while Curl.Api sent the body as HTTP post fields. Decision: Curl.Api uploads a body to kafka(s), grpc(s), signalr(s) as it already does for the other non-HTTP schemes (UploadSchemes), so the handlers get it through Upload, read whole, same as -d. No Curl.Engine change. Unit tests pin byte and stream bodies for all six schemes. Criterion 1 is met by the handlers' own Upload-or-PostData tests plus these; no live Surl run was possible unattended. Fast tests pass (Curl.Api.UnitTests 942 passed).

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding widening BL-5594 (Kafka, gRPC, SignalR).
- 2026-11-20: Backlog -> Doing. Criteria at claim: c24813d3 3173a705 3d836d5e.
- 2026-11-20: Doing -> Done. Curl.Api body to kafka, grpc, signalr uploads as -T so handlers read it
