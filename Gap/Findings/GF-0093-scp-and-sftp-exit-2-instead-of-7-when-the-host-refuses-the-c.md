---
id: GF-0093
title: scp and sftp exit 2 instead of 7 when the host refuses the connection
area: protocols
platform: macos
key: protocols:ssh-exit-code
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [protocols:scp, protocols:sftp]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
task: BL-2336
tasks: [BL-2336]
---
# GF-0093 - scp and sftp exit 2 instead of 7 when the host refuses the connection

## Summary

Curl differs from upstream curl in protocols on macos: scp and sftp exit 2 instead of 7 when the host refuses the connection.

## Evidence

Both schemes are listed by Curl and the reference. For curl scp://127.0.0.1:1/ and curl sftp://127.0.0.1:1/ the reference exits 7 (expected 'listed, exit 7'); Curl exits 2 (actual 'listed, exit 2'). macOS measurement, curl 8.22.0 built with OpenSSL. Reproduce on macOS from the repository root: `curl scp://127.0.0.1:1/; echo $?` and `curl sftp://127.0.0.1:1/; echo $?`, once with the reference curl and once with Curl's build; expect 7 from the reference and 2 from Curl.

## Suggestion

In Curl.Protocol.Ssh.UnitLibrary, make a refused or unreachable connection on scp and sftp return CurlExitCode.CouldntConnect (7), as curl does. SshProtocolHandler.cs (around line 165) passes the connector's ExitCode through unchanged, so the 2 comes from the connector Curl.Console's CurlComposition gives the SSH handler, or from SshTransferException.SessionEstablishmentFailed (FailedInit, exit 2), which should only run once a connection exists. Trace which one returns 2 on a refused connect, then pin exit 7 for both schemes in Curl.Protocol.Ssh.UnitTests. If the trace leads to the composition, add Curl.Console and Curl.Console.UnitTests to touches.

## Measurements

- 2026-10-15_0751: 2 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-protocols.
- 2026-10-15_0900: Filed BL-2336.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on macos.
