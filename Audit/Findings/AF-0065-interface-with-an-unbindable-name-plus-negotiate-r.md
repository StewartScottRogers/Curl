---
id: AF-0065
title: --interface with an unbindable name plus --negotiate reports the Negotiate SSPI failure instead of curl's interface-binding failure
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Curl.Console/CurlCommandRunner.cs:--interface:stderr
reproduction: none
task: BL-1684
tasks: BL-1684
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0065 - --interface with an unbindable name plus --negotiate reports the Negotiate SSPI failure instead of curl's interface-binding failure

## Summary

Low finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs`: --interface with an unbindable name plus --negotiate reports the Negotiate SSPI failure instead of curl's interface-binding failure. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 264985340, count 300: case 220 (--ftp-method GET --interface @f.txt --negotiate URL) differed in stderr only. Reduced to `--interface @f.txt --negotiate URL`; --interface @f.txt alone and --negotiate alone are the same as curl. Both exit 45. curl stderr: 'curl: (45) Failed to connect to 127.0.0.1:PORT after N ms: Failed binding local connection end'. Curl stderr: 'curl: (45) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package'. Curl sets up the Negotiate security context before binding the local interface, where curl binds first and never reaches authentication; scripts that parse the message see different text. Phase 2: the Negotiate ADRs (ADR-0176, ADR-0227) do not mention --interface or this order; no ADR records the divergence. Major/Minor under conformance-auditor: a stderr-only wording difference on an uncommon path, mapped to Low.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\af-if"; $a=@('--interface','@f.txt','--negotiate','-s','-S','http://127.0.0.1:50998/'); & ./Record-CurlExchange.ps1 -Port 50998 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50998 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; Get-Content "$o\curl\stderr.txt", "$o\candidate\stderr.txt"
```

- Expected: Both lines: 'curl: (45) Failed to connect to 127.0.0.1:50998 after N ms: Failed binding local connection end'
- Actual: curl: '...Failed binding local connection end'; Curl: 'curl: (45) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package'

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction (--interface @f.txt --negotiate -s -S): both exit 45. curl stderr 'curl: (45) Failed to connect to 127.0.0.1:50998 after 1 ms: Failed binding local connection end'; Curl's stderr is the same except 'after 34 ms'. Curl no longer reports the Negotiate SSPI failure.
- 2026-10-08 | 2026-10-08_2315.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: both stderr files read 'curl: (45) Failed to connect to 127.0.0.1:<port> after <n> ms: Failed binding local connection end'. Both exit 45. Only the elapsed time differs. No Negotiate/SSPI text appears.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the reproduction: both stderr files read 'curl: (45) Failed to connect to 127.0.0.1:50998 after <n> ms: Failed binding local connection end' (only the elapsed time differs), and both exit 45. Neither mentions the Negotiate SSPI failure.
- 2026-10-09 | 2026-10-09_0647.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: both stderr files read 'curl: (45) Failed to connect to 127.0.0.1:50998 after N ms: Failed binding local connection end', apart from the elapsed time. Both exit 45, and there is no Negotiate SSPI message from Curl. Reference curl 8.21.0 Schannel.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
