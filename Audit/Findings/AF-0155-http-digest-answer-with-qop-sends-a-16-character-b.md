---
id: AF-0155
title: HTTP Digest answer with qop sends a 16-character base64 cnonce where curl 8.21.0 sends 32 lower-case hex digits
auditor: conformance
severity: Medium
status: accepted
reason: 
key: conformance:Curl.Authentication.UnitLibrary/DigestClientNonce.cs:CreateRandom:request
reproduction: none
task: BL-2048
tasks: BL-2048
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0155 - HTTP Digest answer with qop sends a 16-character base64 cnonce where curl 8.21.0 sends 32 lower-case hex digits

## Summary

Medium finding from the conformance auditor at `Curl.Authentication.UnitLibrary/DigestClientNonce.cs:14`: HTTP Digest answer with qop sends a 16-character base64 cnonce where curl 8.21.0 sends 32 lower-case hex digits. Reported by an auditor flagged unreliable in 2026-10-11_0051.md.

## Evidence

Location: `Curl.Authentication.UnitLibrary/DigestClientNonce.cs:14`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Not found by the differential tool (seed 586388576, 300 cases, 0 differences); found by a targeted probe, reduced to: -u user:pw --digest -s http://127.0.0.1:PORT/ against a 401 'WWW-Authenticate: Digest realm="r", nonce="abc", qop="auth"' then a 200. Request bytes differ only in cnonce (and so response): curl sent 'Authorization: Digest username="user",realm="r",nonce="abc",uri="/",cnonce="889edd63675c2328372ba0489df3b38f",nc=00000001,response="...",qop="auth"'; Curl sent the same line with cnonce="pSzCeMrpaD8cAeqA". Three runs each: curl always 32 lower-case hex, Curl always 16 base64 characters (one contained '+'). Without qop both requests are identical. The code: line 14 'public static string CreateRandom() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));', doc comment line 6 'Creates the client nonce (cnonce) of a Digest answer as curl 8.21.0 does' - false for the matched Schannel build; a hex CreateRandomHex exists at line 21 for SASL. A strict or logging server sees a cnonce of a different length and alphabet from the binary scripts expect. Phase 2: partly explained by ADR-0025 ('a cnonce of 12 random bytes in base64'), but that ADR pairs the base64 cnonce with the OpenSSL-format answer (', ' separators, unquoted qop); Curl now sends the reference's SSPI format (no blanks, qop quoted) with the OpenSSL build's cnonce, a mix neither build sends, so the divergence is not recorded as it stands.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\ac-cn"; $r=@('HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm="r", nonce="abc", qop="auth"\r\nContent-Length: 0\r\n\r\n','HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n'); $a=@('-u','user:pw','--digest','-s','http://127.0.0.1:50990/'); & ./Record-CurlExchange.ps1 -Port 50990 -Connections 2 -Response $r -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50990 -Connections 2 -Response $r -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; Select-String -Path "$o\curl\request.bin","$o\candidate\request.bin" -Pattern 'cnonce="([^"]*)"' | ForEach-Object { $_.Matches[0].Groups[1].Value }
```

- Expected: Two 32-digit lower-case hex values (curl's and Curl's), e.g. f81aa18d322ca83d71b7cd6b2fca174c
- Actual: f81aa18d322ca83d71b7cd6b2fca174c then M9ayikB4fH43+3Fo - Curl's cnonce is 16 base64 characters

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
