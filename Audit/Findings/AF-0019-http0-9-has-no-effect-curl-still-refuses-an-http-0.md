---
id: AF-0019
title: --http0.9 has no effect: Curl still refuses an HTTP/0.9 reply with exit 1 where curl prints it and exits 0
auditor: conformance
severity: High
status: closed
reason: Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
key: conformance:Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs:--http0.9:exit-code
task: BL-1275
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed: 2026-10-07
closed-by: 2026-10-07_0844.md
---
# AF-0019 - --http0.9 has no effect: Curl still refuses an HTTP/0.9 reply with exit 1 where curl prints it and exits 0

## Summary

High finding from the conformance auditor at `Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs:60`: --http0.9 has no effect: Curl still refuses an HTTP/0.9 reply with exit 1 where curl prints it and exits 0.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs:60`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found by a hand-picked case beside the differential run (seed 863948043). Smallest command line: curl --http0.9 -sS http://127.0.0.1:PORT/ against a server that answers 'just text'. curl: exit 0, stdout 'just text'. Curl: exit 1, stdout empty, stderr 'curl: (1) Received HTTP/0.9 when not allowed'. Cause: CommandLineOptionTable.cs:324 sets CommandLineOptions.AllowHttp09Reply, but outside the --libcurl source writer (LibcurlSourceCode.TransferOptions.cs:56) nothing reads it. HttpStatusLine.Parse (line 60) and RejectHttp09 (line 100), reached from HttpLineReader.cs:77, reject any line that does not start with HTTP/ every time. Without --http0.9 both binaries agree (exit 1). Phase 2: no ADR under Documentation/Planning/Decisions mentions http0.9; this divergence is not recorded anywhere.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', '.\Curl.Console\bin\Release\net10.0\curl.exe'))) { $d = "$env:TEMP\h09\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48299 -Curl $x[1] -OutDirectory $d -Response 'just text' -CurlArgs '--http0.9', '-sS', 'http://127.0.0.1:48299/' *> $null; '{0}: exit {1}, stdout [{2}], stderr [{3}]' -f $x[0], (Get-Content "$d\exitcode.txt"), (Get-Content -Raw "$d\stdout.bin"), "$(Get-Content -Raw "$d\stderr.txt")".Trim() }
```

- Expected: curl: exit 0, stdout [just text], stderr []  /  candidate: exit 0, stdout [just text], stderr []
- Actual: curl: exit 0, stdout [just text], stderr []  /  candidate: exit 1, stdout [], stderr [curl: (1) Received HTTP/0.9 when not allowed]

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Ran the reproduction against curl 8.21.0 Schannel (TEMP redirected to the scratch folder): 'curl: exit 0, stdout [just text], stderr []' and 'candidate: exit 0, stdout [just text], stderr []'. Curl now accepts the HTTP/0.9 reply under --http0.9.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction: 'curl: exit 0, stdout [just text], stderr []' and 'candidate: exit 0, stdout [just text], stderr []'. Curl now prints the HTTP/0.9 reply under --http0.9.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
