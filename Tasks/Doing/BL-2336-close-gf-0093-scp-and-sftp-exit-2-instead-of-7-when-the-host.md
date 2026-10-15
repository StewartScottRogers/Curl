---
id: BL-2336
title: Close GF-0093: scp and sftp exit 2 instead of 7 when the host refuses the connection (on macos)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2336 — Close GF-0093: scp and sftp exit 2 instead of 7 when the host refuses the connection (on macos)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0093 (scp and sftp exit 2 instead of 7 when the host refuses the connection), so a later gap analysis measures each of `protocols:scp`, `protocols:sftp` as `match`.

## Context

- Finding: GF-0093, filed by the gap analysis office (ADR-0433).
- Area: protocols. Severity: Medium. Introduced in: not stated upstream.
- Platform: macos, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is macos's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on macos passing a test that pins each item; write those tests platform-neutral or mark them for macos.
- Items: `protocols:scp`, `protocols:sftp`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_PROTOCOLS.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both schemes are listed by Curl and the reference. For curl scp://127.0.0.1:1/ and curl sftp://127.0.0.1:1/ the reference exits 7 (expected 'listed, exit 7'); Curl exits 2 (actual 'listed, exit 2'). macOS measurement, curl 8.22.0 built with OpenSSL. Reproduce on macOS from the repository root: `curl scp://127.0.0.1:1/; echo $?` and `curl sftp://127.0.0.1:1/; echo $?`, once with the reference curl and once with Curl's build; expect 7 from the reference and 2 from Curl.

Suggestion, copied from the finding:

In Curl.Protocol.Ssh.UnitLibrary, make a refused or unreachable connection on scp and sftp return CurlExitCode.CouldntConnect (7), as curl does. SshProtocolHandler.cs (around line 165) passes the connector's ExitCode through unchanged, so the 2 comes from the connector Curl.Console's CurlComposition gives the SSH handler, or from SshTransferException.SessionEstablishmentFailed (FailedInit, exit 2), which should only run once a connection exists. Trace which one returns 2 on a refused connect, then pin exit 7 for both schemes in Curl.Protocol.Ssh.UnitTests. If the trace leads to the composition, add Curl.Console and Curl.Console.UnitTests to touches.

## Acceptance criteria

- [ ] `protocols:scp`: Curl answers what curl 8.22.0 answers on macos, `listed, exit 7`, so the item measures `match`.
- [ ] `protocols:sftp`: Curl answers what curl 8.22.0 answers on macos, `listed, exit 7`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on macos) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
