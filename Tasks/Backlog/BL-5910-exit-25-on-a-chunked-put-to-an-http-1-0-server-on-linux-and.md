---
id: BL-5910
title: Exit 25 on a chunked PUT to an HTTP/1.0 server on Linux and macOS (upstream tests 1072 and 1073)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpRequestFraming.cs, Curl.Original.Protocol.Http.UnitLibrary/Http1VersionSeen.cs, Curl.Original.Protocol.Http.UnitTests/HttpRequestFraming]
requirement: none
created: 2026-11-24
completed:
---
# BL-5910 — Exit 25 on a chunked PUT to an HTTP/1.0 server on Linux and macOS (upstream tests 1072 and 1073)

## Goal

`curl -T -` to a server that answers HTTP/1.0 exits 25 (`CURLE_UPLOAD_FAILED`) on Linux and macOS, as upstream curl 8.21.0 does and as the port already does on Windows, so upstream test cases 1072 and 1073 pass on every platform.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 29 (Surl `Upstream-Test-Cases.md` finding UTC-5). Seen on linux-x64 (run 44044472038) and osx-arm64 (run 45412760425) at port commit `74f3f45` (2026-11-16). Check it still reproduces on the current branch first; the port's own CI runs the upstream cases on Linux and macOS.

Cases: `test1072` "HTTP chunked PUT to HTTP 1.0 server with authorization" (`http://%HOSTIP:%HTTPPORT/1072 -T - -u testuser:testpass --anyauth`) and `test1073` "HTTP chunked PUT to HTTP 1.0 server with redirect" (`http://%HOSTIP:%HTTPPORT/1073 -T - -L`), tag `curl-8_21_0`. Upstream exits 25 as each `<errorcode>` says. The port exits 65 (`CURLE_SEND_FAIL_REWIND`) on Linux and macOS, 25 on Windows. The platform split suggests an ordering race: on the second request the port first finds stdin can't be rewound (65), where it should first find that chunked upload is impossible to an HTTP/1.0 server (25), or a socket-close timing difference. Related: BL-2130. Start at `HttpRequestFraming.cs` and `Http1VersionSeen.cs`.

## Acceptance criteria

- [ ] The decision to fail with 25 comes before any rewind check, whatever the platform's timing, pinned by a unit test with a test-double connection that answers HTTP/1.0 to a chunked `-T -` (with `--anyauth` and with `-L`).
- [ ] Upstream cases 1072 and 1073 pass in the port's upstream-case runner (CI on Linux and macOS).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 29.
