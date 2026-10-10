---
id: BL-1899
title: Emulate the SSH transport layer of upstream's test sshd in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1934, BL-1935]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1899 — Emulate the SSH transport layer of upstream's test sshd in the case runner

## Goal

The runner contains a hand-written SSH transport server (version exchange, key exchange, host key, encryption and MAC) for upstream's SCP and SFTP cases.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 42 SSH cases are skipped (%SSHPORT, %USER, %SFTP_PWD, %SCP_PWD). Upstream uses OpenSSH sshd; this is a minimal hand-written server in C#, BCL only. First read Curl.Protocol.Ssh.UnitLibrary (its client transport may hold reusable packet, key exchange and cipher code; the Conformance library references no protocol library today, so reuse by project reference only after amending that rule in the Notes and CLAUDE.md; otherwise use Curl.Cryptography.UnitLibrary primitives and the BCL). Implement: identification string exchange, KEXINIT, curve25519-sha256 (or whatever Curl's client offers first), a host key made for the tests, the cipher and MAC the client offers, NEWKEYS, service request. If the code grows large, put it in Curl.Conformance.SshServer.UnitLibrary with a .UnitTests twin and say so in touches, the solution file and the Notes. No authentication yet. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] A unit test connects Curl's own SSH client transport (from Curl.Protocol.Ssh.UnitLibrary, in memory) to the new server and completes key exchange up to the service request.
- [ ] Host key fingerprints are stable per run so --hostpubmd5 and --hostpubsha256 cases can name them (document how).
- [ ] The CLAUDE.md of the library holding the server describes the transport.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build -warnaserror` is clean for every touched project and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-10-09 (lane 2): Split. `Curl.Protocol.Ssh.UnitTests/Fakes/InMemorySshServerSession.cs` already runs a full server-side SSH session, but as ungated test code; every type in `Curl.Protocol.Ssh.UnitLibrary` is `internal`. Decided in ADR-0456 (by Claude under Stewart's delegation): the server goes in a new `Curl.Conformance.SshServer.UnitLibrary` (+ `.UnitTests`) that reuses the SSH client's packet, negotiation, key exchange and packet protection code through `InternalsVisibleTo`, with RFC 8032's first Ed25519 key as a fixed host key so fingerprints are stable. A new project, the `Curl.slnx` edit and the SSH csproj change are outside this task's `touches`, and the port plus 100% coverage did not fit one run's budget, so the work is BL-1934 (scaffold, identification, plain packets) then BL-1935 (KEX, NEWKEYS, service request, fingerprints). What is left here once both are Done: confirm each criterion above against them, tick them, and add a paragraph to `Curl.Conformance.UnitLibrary/CLAUDE.md` pointing at the new library.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Split: waits on BL-1934 (scaffold Curl.Conformance.SshServer.UnitLibrary) and BL-1935 (key exchange to the service request), per ADR-0456
- 2026-10-09: Backlog -> Doing.
