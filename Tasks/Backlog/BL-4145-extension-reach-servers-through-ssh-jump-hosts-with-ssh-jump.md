---
id: BL-4145
title: Extension: Reach servers through SSH jump hosts with --ssh-jump
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095, BL-4328, BL-4329, BL-4330]
touches: [Curl.Extension.SshJump.UnitLibrary, Curl.Extension.SshJump.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4145 — Extension: Reach servers through SSH jump hosts with --ssh-jump

## Goal

Curl provides `--ssh-jump user@host[:port]` for SFTP/SCP and an SSH tunnel for other schemes, as the extension `Curl.Extension.SshJump.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 45 of 63. Specification: RFC 4254 direct-tcpip channels. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.SshJump.UnitLibrary` and `Curl.Extension.SshJump.UnitTests` exist in `/Extensions/SshJump/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `--ssh-jump user@host[:port]` for SFTP/SCP and an SSH tunnel for other schemes.
- [ ] Unit tests check its wire behaviour against named sections of RFC 4254 direct-tcpip channels, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 5): Requeued before any code. The jump host needs a full SSH client (transport, key exchange, host keys, user authentication, connection channels); all of it lives in `Curl.Protocol.Ssh.UnitLibrary` (about 8,300 lines outside SFTP/SCP), which an extension may not reference (ADR-0999). Rather than duplicate it, filed BL-4328 (shared `Curl.Ssh.UnitLibrary` with the transport layer), BL-4329 (keys, host keys, authentication) and BL-4330 (session, connection layer and a `direct-tcpip` channel exposed as an `IConnection`). Once they are Done, this task supplies an `IConnector` through the BL-4095 connector hook that opens a session to the jump host and returns a direct-tcpip channel, so SFTP, SCP and every other scheme run through it, plus the option, ADR and description.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1a58a3c5 2feb1157 41e8e2fd e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4328, BL-4329 and BL-4330, which move the SSH client into a shared Curl.Ssh.UnitLibrary an extension may reference
