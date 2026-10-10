---
id: AF-0085
title: -v omits curl's '{ [0 bytes data]' line when a read-to-close HTTP body ends with an empty read (e.g. --ignore-content-length on an empty body)
auditor: conformance
severity: Low
status: accepted
reason:
key: conformance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:--verbose-read-to-close-empty-body:stderr
reproduction: none
task: BL-1763
tasks: BL-1763
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0085 - -v omits curl's '{ [0 bytes data]' line when a read-to-close HTTP body ends with an empty read (e.g. --ignore-content-length on an empty body)

## Summary

Low finding from the conformance auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:414`: -v omits curl's '{ [0 bytes data]' line when a read-to-close HTTP body ends with an empty read (e.g. --ignore-content-length on an empty body).

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:414`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 70867401: case 245 (--haproxy-clientip - --verbose --follow --ignore-content-length URL) differs in stderr only; exit codes 0/0, stdout identical. Reduced by dropping one option at a time to '-v --ignore-content-length URL' (dropping --ignore-content-length or -v removes the difference). Against the canned 'HTTP/1.1 200 OK, Content-Length: 0' response, curl writes '{ [0 bytes data]' after the '< ' empty line and before the meter rows; Curl writes nothing there (diff: '16d15 < { [0 bytes data]'). Cause: the read-to-close loop in HttpResponseBodyReader (lines 332-340) ends on read == 0 without a data event, and ReportReceived (lines 414-420) reports nothing for an empty span. Phase 2: no ADR records this as deliberate.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\af-icl"; $a=@('-v','--ignore-content-length','http://127.0.0.1:50997/'); & ./Record-CurlExchange.ps1 -Port 50997 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50997 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl: ' + @(Select-String -Path "$o\curl\stderr.txt" -SimpleMatch 'bytes data]').Count + ' / Curl: ' + @(Select-String -Path "$o\candidate\stderr.txt" -SimpleMatch 'bytes data]').Count
```

- Expected: curl: 1 / Curl: 1
- Actual: curl: 1 / Curl: 0

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: 'curl: 1 / Curl: 1'. Both -v traces contain one 'bytes data]' line, and both exit 0.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the reproduction: 'curl: 1 / Curl: 1'. Diffing the two stderr files shows only the ephemeral source port on the '* Established connection' line.
- 2026-10-09 | 2026-10-09_0647.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: 'curl: 1 / Curl: 1'. Curl's -v --ignore-content-length output now carries the '[0 bytes data]' line, and both exit 0. Reference curl 8.21.0 Schannel.
- 2026-10-09 | 2026-10-09_1435.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: 'curl: 1 / Curl: 1' - both -v outputs carry one 'bytes data]' line for --ignore-content-length on the empty body; both exit 0. Reference curl 8.21.0 Schannel.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
