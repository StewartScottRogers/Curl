---
id: BL-4490
title: Conformance: RESP2 and RESP3 and the redis URI scheme for redis:// and rediss://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Redis, Curl.Conformance.Extended.UnitTests/Redis, Documentation/Conformance/Extended/Redis.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4490 — Conformance: RESP2 and RESP3 and the redis URI scheme for redis:// and rediss://

## Goal

Curl's `redis://` and `rediss://` client is measured against the Redis serialization protocol (RESP2 and RESP3),
the `HELLO`/`AUTH`/`SELECT` handshake and the IANA redis URI scheme through the real curl command line, every client
requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specifications: "Redis serialization protocol specification" (redis.io/docs/latest/develop/reference/protocol-spec:
RESP2 and RESP3 types: simple string, error, integer, bulk string, array, null, boolean, double, big number, bulk
error, verbatim string, map, attribute, set, push; RESP2 `-1` nulls; client requests as arrays of bulk strings;
`HELLO 3` and protocol negotiation), the `HELLO`, `AUTH` and `SELECT` command references, and the `redis` and
`rediss` URI schemes (IANA provisional registrations, www.iana.org/assignments/uri-schemes/prov/redis and
`.../rediss`: user info, host, port 6379, database number in the path). Rendering follows `redis-cli --raw`
(ADR-1079): that is the library's own choice, so map it as `n/a: output format, not protocol` unless the protocol
spec says otherwise.

Library: `Curl.Extended.Protocol.Redis.UnitLibrary` (`CLAUDE.md`: `Client/RedisUrlCommand`, `RedisClientFlow`,
`Wire/RespReplyReader`, `RespCommandEncoding`, `RespReplyRendering`; ADR-1079). URL path `/[db]/command/argument/...`;
`-d` appends one argument.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Redis:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference server implements RESP from the specification itself and references no
`Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task touches only its
own conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a defect in
the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Redis conformance: <requirement>`
(pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a
focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Redis.md` gives every requirement of RESP2, RESP3, the handshake commands
      and the URI schemes that binds a client a `Redis:` ID mapped to a test or to `n/a: <reason>`; the harness's
      matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Redis/` run curl in process for `redis://` and `rediss://` and
      assert wire bytes, stdout, stderr and exit code for at least: every RESP3 reply type, nested aggregates, an
      attribute before a reply, a push frame before the reply, RESP2 nulls from a server that refuses `HELLO 3`,
      `AUTH` from `-u` and from the URL (with and without a user name), `SELECT` from the path, an error reply, binary
      arguments in bulk strings, and a reply split across reads.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Redis conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/Redis/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Reference server: `Curl.Conformance.Extended.UnitLibrary/Redis/RedisReferenceServer.cs` reads each command with
  `RespRequestReader` (arrays of bulk strings only, from the RESP spec; anything else throws) and writes the reply
  chunks a case scripts, one write per chunk, so a case splits a reply across reads. It references no extension.
- Cases (`Curl.Conformance.Extended.UnitTests/Redis/`): `RedisReplyTypeCases` (every RESP3 type), `RedisFramingCases`
  (nested aggregates, attribute, push, split reply, binary arguments), `RedisHandshakeCases` (HELLO, RESP2 fallback,
  AUTH from `-u` and the URL with and without a user, NOAUTH, WRONGPASS, SELECT), `RedisUriCases` (port, `rediss://`
  over the TLS route); each asserts the wire bytes, stdout, stderr and exit code. `RedisReferenceServerTests` covers
  the server's own edges. Matrix: `Documentation/Conformance/Extended/Redis.md`, 39 rows, 3 `n/a`.
- Choice: stderr expectations are written with `\n` and compared with `Environment.NewLine`, as the other protocols'
  cases do, so they hold on every platform. Printed text follows ADR-1079 and is pinned as measured.
- Known failures (filed High): `Redis:RESP-Push-1` BL-4583 (a push frame before the reply is printed as the reply);
  `Redis:HELLO-2` and `Redis:RESP2-Null-2` BL-4584 (an error to `HELLO 3` fails with exit 8 instead of going on in RESP2).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`: 0 failing members.
  `dotnet build Curl.slnx -warnaserror` clean; fast tests green; Curl.Conformance.Extended.UnitTests 565 passed.
  CI on Linux and macOS: the cases use no path, drive letter or OS text, so they are platform-neutral.
## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 990b7c72 e36b79d3 0e3b4bcc d8e3fb0e 46790798 ef6c9026.
- 2026-11-02: Doing -> Done. Redis conformance matrix, reference server and cases landed; 3 known failures filed as BL-4583, BL-4584
