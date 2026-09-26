---
id: BL-259
title: Say in TransferReport.RequestSize that curl counts the body bytes sent
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-175]
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-09-26
completed:
---
# BL-259 — Say in TransferReport.RequestSize that curl counts the body bytes sent

## Goal

`TransferReport.RequestSize`'s doc comment says what curl 8.21.0 measures: the request head and the body bytes sent.

## Context

- The doc comment says "the bytes of every request header block sent, body excluded", but curl 8.21.0 reports `%{size_request}` 151 for `curl -d x=1 http://127.0.0.1:18082/` (148 head bytes + 3 body bytes), 150 for `-X PUT -d x=1`, and 1048753 for a 1048577-byte body with `Expect: 100-continue` (176 + 1048577). Measured in BL-175; `HttpProtocolHandler` already reports it this way.
- A misaligned doc is a defect under "say what it does, do what it says".

## Acceptance criteria

- [ ] The XML doc of `TransferReport.RequestSize` in `Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs` says it counts the head and the body bytes sent, the source of `%{size_request}`.
- [ ] `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-09-26: Created.
