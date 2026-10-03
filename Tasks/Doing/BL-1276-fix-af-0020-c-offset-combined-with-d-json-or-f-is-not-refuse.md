---
id: BL-1276
title: Fix AF-0020: -C <offset> combined with -d, --json or -F is not refused: curl exits 2 'cannot mix --continue-at with --data/--form', Curl sends the request
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1276 — Fix AF-0020: -C <offset> combined with -d, --json or -F is not refused: curl exits 2 'cannot mix --continue-at with --data/--form', Curl sends the request

## Goal

The defect the audit office reported as AF-0020 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0020 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0020-c-offset-combined-with-d-json-or-f-is-not-refused.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:1286`

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:1286`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 863948043, case 271 (--remote-name-all --continue-at 10 --retry-delay 0 --data @data.txt URL): curl exit 2, stderr 'curl: cannot mix --continue-at with --data' then 'curl: (2) Failed initialization', and no request. Curl sent 'Range: bytes=10-' with the POST and went on with the transfer. Reduced to -sS -C 10 -d x URL. Also -C 10 -F a=b (curl: 'cannot mix --continue-at with --form', exit 2) and -C 10 --json {} (curl: '... with --data', exit 2). Curl does not refuse any of them. Its exit then depends on the server: 0 in a probe, 56 under Record-CurlExchange because of the recorder defect below. 'cannot mix' appears nowhere in production code. SetResumeFrom (line 1286) checks only --range and --remove-on-error. -C - with -d is accepted by both. Phase 2: no ADR records this divergence.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', '.\Curl.Console\bin\Release\net10.0\curl.exe'))) { foreach ($b in @('-d', 'x'), @('-F', 'a=b')) { $d = "$env:TEMP\cat\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48296 -Curl $x[1] -OutDirectory $d -CurlArgs (@('-sS', '-C', '10') + $b + 'http://127.0.0.1:48296/') *> $null; '{0} -C 10 {1}: exit {2}, stderr [{3}]' -f $x[0], ($b -join ' '), (Get-Content "$d\exitcode.txt"), "$(Get-Content -Raw "$d\stderr.txt")".Trim() } }
```

- Expected: candidate as curl: -C 10 -d x: exit 2, stderr [curl: cannot mix --continue-at with --data / curl: (2) Failed initialization]; -C 10 -F a=b: exit 2, stderr [curl: cannot mix --continue-at with --form / curl: (2) Failed initialization]
- Actual: curl: as expected. candidate -C 10 -d x: exit 56, stderr [curl: (56) Recv failure: Connection was reset]; candidate -C 10 -F a=b: exit 56, the same. The request with Range: bytes=10- was sent.

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
