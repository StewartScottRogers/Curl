---
id: AF-0425
title: DarwinFastOpenConnect.TryConnect, documented to return the socket or null, throws SocketException when dup fails
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs:TryConnect:misleading-name
reproduction: none
task: BL-3473
tasks: BL-3473
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed: 2026-10-24
closed-how: reliable-reaudit
closed-by: 2026-10-24_2106.md
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

- 2026-10-24 | 2026-10-24_2106.md | reproduces: no | Ran the reproduction: DarwinFastOpenConnect.cs:41 '<exception', :45 'Socket? TryConnect', :74 'throw new SocketException'. The doc comment now carries '<exception cref="SocketException">' (lines 41-44): connectx connected but the duplicate failed (EMFILE), and socket is left as it was. That matches the throw on line 74, so the doc no longer claims only a socket-or-null outcome.

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces.
