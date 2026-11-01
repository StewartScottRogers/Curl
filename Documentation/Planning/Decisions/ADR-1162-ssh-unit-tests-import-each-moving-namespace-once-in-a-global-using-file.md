# ADR-1162: The SSH unit tests import each moving namespace once, in a global using file of its own

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4367
- Decided by Claude under Stewart's delegation

## Context

BL-4328 moves the SSH transport layer folder by folder (Compression, PacketProtection, Negotiation, Transport,
KeyExchange, HostKeys, Keys, Authentication, Connection) from `Curl.Original.Protocol.Ssh.UnitLibrary` into
`Curl.Ssh.UnitLibrary`, changing each folder's namespace from `Curl.Original.Protocol.Ssh.<Folder>` to `Curl.Ssh.<Folder>`.
`Curl.Original.Protocol.Ssh.UnitTests` already held one file per test class and fake, but 84 of its files carried
`using Curl.Original.Protocol.Ssh.<Folder>;` lines (159 in all), so every move task had to edit test files across the whole
project and named the whole project in its `touches`. Six tasks did, and the board ran them one at a time.

## Decision

- `Curl.Original.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Original.Protocol.Ssh.<Folder>.cs` holds the single line
  `global using Curl.Original.Protocol.Ssh.<Folder>;` for each of the nine folders above, and no test file imports those
  namespaces itself. Moving a folder changes its one file to `global using Curl.Ssh.<Folder>;`; types moved from the
  protocol library's root to `Curl.Ssh` get a new `GlobalUsings/Curl.Ssh.cs`.
- Namespaces that stay in the protocol library (`Fakes`, `Scp`, `Sftp`) keep their per-file using lines.
- `Curl.Original.Protocol.Ssh.IntegrationTests` links the whole `GlobalUsings` folder beside the fakes it already links, so a
  new or changed global using file reaches it with no edit there.
- The move tasks' `touches` name their own folders, global using files and the `Fakes` folder instead of the whole
  test project.

## Consequences

A move task touches only its own files in the test project, so `task-board.ps1 hotspots` no longer counts five or
more tasks on it. Every SSH test file sees all nine namespaces; a type name defined in two of them would be
ambiguous and fail the build, which would show at once.
