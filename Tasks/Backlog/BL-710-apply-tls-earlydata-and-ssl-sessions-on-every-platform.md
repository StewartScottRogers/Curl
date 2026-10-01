---
id: BL-710
title: Apply --tls-earlydata and --ssl-sessions on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-701, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-710 — Apply --tls-earlydata and --ssl-sessions on every platform

## Goal

`--ssl-sessions <file>` loads TLS sessions from the file before the transfers and saves them after, in curl 8.21.0's file format, and `--tls-earlydata` sends the request as 0-RTT early data on a resumed session, as curl does, on every platform.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Parsing: BL-618; routing: BL-617's ADR and BL-708; session records and early data: BL-701.
- The file format is curl's (`lib/vtls/vtls_spack.c` and `docs/cmdline-opts/ssl-sessions.md` at tag `curl-8_21_0`: read them and record the format in the XML docs); sessions whose TLS backend data curl cannot reuse are skipped as curl skips them.
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1 -Tls -k`: two runs sharing `--ssl-sessions f` (the second resumes), `--tls-earlydata` on the second run, and `-v` lines for both; the file bytes copied into Notes.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests pin the session file bytes written for a fixed session, a resumed second transfer with the early-data request bytes, the `-v` lines, and a corrupt file handled as curl handles it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-10-01 (lane 4), partial; returned to Backlog when the run's budget ran out. The code is uncommitted in the lane worktree, so the shift stashes it. Pick it up from there.
  - Measurement: this machine has only the Schannel build (curl 8.21.0), which refuses `--ssl-sessions` with exit 2 (ADR-0151). No SSLS-EXPORT OpenSSL build is available, so the format comes from curl-8_21_0's `lib/vtls/vtls_spack.c`, `lib/vtls/vtls_scache.c`, `src/tool_ssls.c` and `src/tool_msgs.c`. File: two `#` comment lines, then one `base64(salt32 ‖ HMAC-SHA256(salt, peer key)):base64(pack)` line per session. Pack: `01`, `04` u16-length ticket (OpenSSL `i2d_SSL_SESSION`), `02` u16 IETF id, `03` u64 valid-until, `05` u16-length ALPN, `06` u32 max early data, `07` u16-length QUIC parameters. The warnings: `unrecognized line N in SSL session file F`, `invalid shmax base64 encoding in line N`, `invalid sdata base64 encoding in line N: X`, `import of session from line N rejected(26|43)`, `Failed to create SSL session file F`, and under -v `Note: SSL session file does not exist (yet?): F`. Load happens before the transfers and save after them, whatever the result. At most 2 sessions are kept per peer, and a TLS 1.3 session is taken out when offered.
  - Done in the working tree (it builds clean and the fast tests are all green): `Curl.Networking.UnitLibrary/TlsSessionPacking.cs`, `TlsSessionCache.cs` (peer key `host:port[:NO-VRFY-PEER:NO-VRFY-HOST][:VRFY-STATUS][:CA-<path>]:IMPL-Curl:G`; curl's keys name `IMPL-OpenSSL/<version>`, so cross-tool lines are kept unused and written back), a `--ssl-sessions` routing row, and `HandBuiltTlsProvider` offering `Take(peerKey)` and tracking received tickets. In `Curl.Console`: `TlsSessionFileLines` and the runner's load and save (`CurlCommandRunner` `tlsSessions`, `CurlComposition`). Tests: `TlsSessionPackingTests`, `TlsSessionCacheTests`, `HandBuiltTlsProviderTests.SslSessions`, `TlsClientRoutingTests` row, `TlsSessionFileLinesTests`.
  - Still to do: (1) the ADR (ADR-0313 named in the doc comments: peer key choice, port taken from `IConnection.RemoteEndPoint` (443 when unknown, the proxy's port through a tunnel), TLS 1.2 sessions not kept); (2) `--tls-earlydata`: a routing row, and sending the first request through `Tls13ClientConnection.ConnectWithEarlyDataAsync`, which needs the request bytes before the handshake (a deferred-handshake connection) - consider filing it as its own task; (3) `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Console` printed an empty table, so find the right `-Library` form and confirm 100%/100%; (4) a resumed-handshake test through the provider (the `Fakes` TLS 1.3 test server sends no ticket); (5) Notes and README for ADR-0313 and the `Curl.Networking` CLAUDE.md routing paragraph.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Backlog. Run budget ran out mid-task: --ssl-sessions is implemented and tested in the stashed working tree; the ADR, --tls-earlydata and the coverage confirmation remain (see Notes)
