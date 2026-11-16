---
id: BL-5296
title: Honour -u in the grpc client
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcBasicCredentialsTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5296 — Honour -u in the grpc client

## Goal

`curl -u user:password grpc://host/...` sends the login as an `authorization: Basic` header, as the port does for every other scheme that takes `-u`.

## Context

Filed from Surl's Phase 7 measurement of the port's extended clients (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 19 (github.com/StewartScottRogers/Surl). Port measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), Windows x64. Upstream curl has no `grpc` scheme, so this is judged by the specification and curl 8.21.0's `-u` contract.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Extended.md`, EXT-2 (Surl task BL-446).
- **Port (defect):** `-u` is ignored; a login only reaches the server through `-H "Authorization: Basic ..."`.
- **What decides:** curl 8.21.0's `-u, --user` (https://curl.se/docs/manpage.html): user and password for server authentication, sent as Basic by default over HTTP; gRPC runs over HTTP/2, so the same `authorization` field applies.
- **Where to start:** `Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs`.

## Acceptance criteria

- [ ] A port test where the server demands Basic credentials passes with `-u user:password`, and the request carries `authorization: Basic <base64 of user:password>`.

## Notes

## Log

- 2026-11-16: Created.
