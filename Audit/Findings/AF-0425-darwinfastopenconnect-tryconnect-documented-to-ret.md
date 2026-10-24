---
id: AF-0425
title: DarwinFastOpenConnect.TryConnect, documented to return the socket or null, throws SocketException when dup fails
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs:TryConnect:misleading-name
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0425 - DarwinFastOpenConnect.TryConnect, documented to return the socket or null, throws SocketException when dup fails

## Summary

Low finding from the truthfulness auditor at `Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs:70`: DarwinFastOpenConnect.TryConnect, documented to return the socket or null, throws SocketException when dup fails. Reported by an auditor flagged unreliable in 2026-10-24_0845.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs:70`

TryConnect (line 41) is documented '<returns>The connected socket, or <see langword="null" />.</returns>' and its summary says connectx's refusal returns null so the caller connects as usual. No <exception> tag is given. Yet line 70: 'throw new SocketException((int)SocketError.TooManyOpenSockets);' when Duplicate(descriptor) < 0 (EMFILE). A Try-named method that throws, with the throw absent from its doc comment.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs -Pattern 'Socket\? TryConnect', 'throw new SocketException', '<exception' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Either no throw inside TryConnect, or an <exception cref="SocketException"> line documenting it.
- Actual: DarwinFastOpenConnect.cs:41 (TryConnect) and DarwinFastOpenConnect.cs:70 (throw new SocketException); no <exception> tag.

## Re-audits

## Log

- 2026-10-24: filed proposed.
