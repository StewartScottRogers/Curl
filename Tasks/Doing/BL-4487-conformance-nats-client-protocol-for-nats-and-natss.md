---
id: BL-4487
title: Conformance: NATS client protocol for nats:// and natss://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Nats, Curl.Conformance.Extended.UnitTests/Nats, Documentation/Conformance/Extended/Nats.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4487 — Conformance: NATS client protocol for nats:// and natss://

## Goal

Curl's `nats://` and `natss://` client is measured against the NATS client protocol through the real curl command
line, every client requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix
task.

## Context

Specification: NATS client protocol (docs.nats.io/reference/reference-protocols/nats-protocol): INFO (fields the
client must honour: `max_payload`, `auth_required`, `tls_required`, `headers`), CONNECT (`verbose`, `pedantic`,
`user`/`pass`, `auth_token`, `name`, `lang`, `version`, `protocol`), PUB, HPUB if used, SUB, UNSUB with max msgs,
MSG and HMSG parsing, PING/PONG, +OK and -ERR, subject naming rules (tokens, wildcards not allowed in PUB subjects),
CRLF framing, and request/reply with an inbox subject. The document has few RFC keywords: treat each "must", "should"
and each documented field the client writes or relies on as a requirement.

Library: `Curl.Extended.Protocol.Nats.UnitLibrary` (no `CLAUDE.md`; flat: `NatsProtocolHandler`, `NatsSession`,
`NatsConnect`, `NatsServerInfo`, `NatsMessageLine`, `NatsWireReader`, `NatsSubject`, `NatsOperations`; ADR-1065).
Port 4222; `natss://` is TLS from the first byte. Without `-d` it subscribes and writes each payload and a newline
until the server closes or `?count=N`; `-d` publishes; `?request` publishes with a reply subject and writes the
reply; `-u` gives user and password, or a token alone.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Nats:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference server implements the protocol from the specification itself and
references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Nats conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Nats.md` gives every requirement of the client protocol that binds a client
      a `Nats:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Nats/` run curl in process for `nats://` and `natss://` and
      assert wire bytes, stdout, stderr and exit code for at least: CONNECT fields for user/password, token and none,
      a publish over `max_payload`, a PING from the server answered with PONG, `-ERR` at connect and after PUB, MSG
      and HMSG with payloads containing CR LF, `?count=N` with UNSUB, request/reply with a unique inbox, a subject
      with a wildcard or empty token refused, and INFO split across reads.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Nats conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Nats/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f369708b eb1aead7 96eae08c d8e3fb0e 3a5499b1 ef6c9026.
