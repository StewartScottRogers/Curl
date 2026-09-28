---
id: BL-667
title: Decide which hand-built libraries a protocol library may reference
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-667 — Decide which hand-built libraries a protocol library may reference

## Goal

An ADR states which non-protocol libraries holding hand-built pieces the BCL lacks (`Curl.Cryptography`, `Curl.Ntlm`, `Curl.Kerberos`, `Curl.Tls`, `Curl.Quic`, `Curl.Http2`, `Curl.Http3`, each `.UnitLibrary`) a `Curl.Protocol.*.UnitLibrary` may reference, what those libraries may reference themselves, and what stays forbidden (a protocol referencing another protocol, `Curl.Networking`, `Curl.Console` or anything that opens a socket).

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", Stewart 2026-09-28): a complete reimplementation of curl; each hand-built piece the BCL does not provide lives in its own `Curl.<Area>.UnitLibrary` with its own `.UnitTests`, same quality gates. Shared hand-built pieces go in non-protocol libraries so protocols still never reference each other (SSH needs `Curl.Cryptography`, SMB needs `Curl.Ntlm`, FTP `--krb` needs `Curl.Kerberos`, HTTP needs `Curl.Http2` and `Curl.Http3`).
- Today `Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` (`ProtocolLibrary_References_OnlyAbstractions`) fails any protocol reference other than `Curl.Protocol.Abstractions.UnitLibrary`, and each `Curl.Protocol.*.UnitLibrary/CLAUDE.md` says "nothing else horizontal". BL-668 changes the test; the tasks that add each reference amend that library's `CLAUDE.md`.
- The hand-built libraries must stay AOT-compatible, BCL-only, and must never construct a `Socket`, `SslStream` or `HttpClient` (they take bytes, spans or injected seams), so protocol tests stay off the network.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", naming the allowed list of hand-built libraries, the reference rules for them (which may reference which; none references a protocol library, `Curl.Networking.UnitLibrary`, `Curl.Core.UnitLibrary` or `Curl.Console`), and that a new hand-built library joins the list by amending this ADR.
- [ ] The ADR names `ProtocolIsolationTests` as the enforcement and BL-668 as the change to it, and states that each protocol library's `CLAUDE.md` names the hand-built libraries it references once it does.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
