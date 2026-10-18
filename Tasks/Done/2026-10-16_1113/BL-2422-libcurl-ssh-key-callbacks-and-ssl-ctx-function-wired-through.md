---
id: BL-2422
title: Libcurl: SSH key callbacks and SSL_CTX_FUNCTION wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2421]
touches: [Libcurl/Callbacks/Keys, Libcurl.UnitTests/Callbacks/Keys, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2422 — Libcurl: SSH key callbacks and SSL_CTX_FUNCTION wired through the engine

## Goal

CURLOPT_SSH_KEYFUNCTION and SSH_HOSTKEYFUNCTION (`SshKey`, `SshKeyMatch`, `SshKeyStatus`) are called by the engine during host-key checks, and CURLOPT_SSL_CTX_FUNCTION answers `CurlCode.NotBuiltIn` as the Schannel build does.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin the match-status outcomes; a Libcurl test pins the SSL_CTX answer.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Touches widened (rule 3): the host-key check lives in `Curl.Protocol.Ssh`'s `SshHostKeyChecker`, which sees only
  `Curl.Protocol.Abstractions`' `SshOptions`, so wiring the callbacks needs a hook on `SshOptions` and a call in the
  checker. No task in Doing on `origin/work/dark-factory` named `Curl.Protocol.Abstractions.UnitLibrary`,
  `Curl.Protocol.Ssh.UnitLibrary` or their tests, so they were added.
- Plan correction: a library handle's perform passed no `SshOptions` at all (`ManagedEasyTransfer.OwnInputsOf`), so
  `CURLOPT_SSH_KNOWNHOSTS`, the fingerprints and the key files never reached the handler; `SshOptionsMapping.OfHandle`
  now builds them for every `scp`/`sftp` perform without `ToolInputs`.
- Design (ADR-0623): `SshOptions.HostKeyAcceptance` and `KnownHostDecision`, new `SshKnownHostMatch`,
  `SshKnownHostVerdict` (numbered as `curl_khmatch` / `curl_khstat`) and `SshKnownHostQuery`; engine adapter
  `CallbackSshHostKeys`; `Libcurl/Callbacks/Keys` sets the SSH functions and answers `NotBuiltIn` for
  `SSL_CTX_FUNCTION`/`_DATA` (which already answered `NotBuiltIn` for want of a row; the row makes it explicit).
- Default taken: the known key's `SshKeyType` is the found key's, as libssh2.c sets both from the remote key type.
- Default taken: `SSH: knownhost entry matches host key` is printed only when the key matched, so a callback that
  accepts a mismatch prints just the `SSH: host check` line.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary` reported 0 failing members (read with `-SkipTestRun` from the coverage of its first run, whose only failure was the unrelated `Curl.Mcp` flake filed as BL-2490; the full fast suite then passed twice). `PassingUpstreamCases.txt` is unchanged.
- Follow-up filed: BL-2489 writes the known-hosts file for `FINE_ADD_TO_FILE` / `FINE_REPLACE`.
- `Libcurl/CLAUDE.md` is outside `touches`; `Curl.Engine.UnitLibrary/CLAUDE.md` gained a section instead (the
  criterion's "or").

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLOPT_SSH_KEYFUNCTION and SSH_HOSTKEYFUNCTION are asked during the SSH host-key check of a library handle's scp/sftp transfer; SSL_CTX_FUNCTION answers NotBuiltIn
