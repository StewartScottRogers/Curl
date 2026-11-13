# ADR-1263 — `smb` and `smbs` stay disabled in every build, and README.md and Protocols.md say so

- **Status:** Accepted
- **Date:** 2026-11-12

Decided by Claude under Stewart's delegation (task BL-5034, finding STD-039).

## Context

ADR-0835 made `smb://` and `smbs://` fail with exit 1 and `Protocol "smb" is disabled` on every platform,
because the targeted curl 8.22.0 has no SMB in either reference build (Schannel on Windows, the OpenSSL
tarball build on Linux and macOS). `EngineComposition` registers a `DisabledProtocolHandler` for both, and
`curl -V` lists neither (ADR-0453). README.md and the SMB row of `Documentation/Protocols.md` still said
the schemes were served and downloaded or uploaded a file. The review's older Linux reference (curl 8.18.0
OpenSSL) lists `smb smbs`, but the targeted version is 8.22.0 (ADR-0479), and the standing rule is to match
the targeted version's platform build.

## Decision

1. `smb` and `smbs` are disabled in all builds, on every OS, as ADR-0835 decided; this ADR confirms it
   against the review's finding and does not serve SMB on Linux or macOS. No code changes.
2. README.md lists `smb` and `smbs` as built but disabled, not among the served schemes, and
   `Documentation/Protocols.md`'s SMB row says every transfer fails with exit 1 and
   `Protocol "smb" is disabled`, and that `curl -V` does not list them.
3. `Curl.Original.Protocol.Smb.UnitLibrary` stays as the SMBv1 client a build with SMB would use.

## Why

- "Match the platform's curl" means the 8.22.0 builds, which refuse SMB; 8.18.0 is not the target.
- Docs must be true of the code as it is now; the code refuses SMB everywhere.

## Consequences

- A later decision to serve SMB (for example if 8.22.0 reference builds gain it) supersedes this ADR and
  files the platform-conditional registration as its own task.
