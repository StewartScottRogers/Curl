---
id: BL-661
title: Report the OpenSSL verify result in %{ssl_verify_result} and %{proxy_ssl_verify_result} off Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-661 — Report the OpenSSL verify result in %{ssl_verify_result} and %{proxy_ssl_verify_result} off Windows

## Goal

Off Windows, `%{ssl_verify_result}` and `%{proxy_ssl_verify_result}` print the X509 verify code the OpenSSL build prints (0 when verified, for example 18 for a self-signed certificate under `-k`, 10 for an expired one), while Windows keeps printing 0 as the Schannel build does (ADR-0043).

## Context

- Conformance audit 2026-09-28, row 44 (Major off Windows, "measure first").
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps both to a constant 0; `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs` already maps chain status to OpenSSL's codes for messages (ADR-0085). The code has to travel from the TLS provider to the report (a `TransferReport` or `ConnectResult` member in Abstractions).
- Measure on Linux or macOS with `Record-CurlExchange.ps1 -Tls`: `-k -w '%{ssl_verify_result}'` against the script's self-signed certificate, the same with `--cacert` for it (verified), and through an HTTPS proxy with `--proxy-insecure -w '%{proxy_ssl_verify_result}'`.

## Acceptance criteria

- [x] Measured first as above; stdout copied into Notes.
- [x] Tests pin each measured value under `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, and 0 under `[OSCondition(OperatingSystems.Windows)]`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurement (2026-09-30)

`Record-CurlExchange.ps1` needs PowerShell and this Windows lane has no Linux pwsh, so the
OpenSSL build was measured in WSL Ubuntu: curl 8.18.0 (OpenSSL 3.5.5) against
`openssl s_server -www -quiet` with certificates made by `openssl req`/`x509` (a self-signed
`localhost` with SAN `DNS:localhost, IP:127.0.0.1`; an expired self-signed one; a `TestCA`
and leaves it signed). Commands `curl -s -o /dev/null <args> -w '...'`, stdout then exit:

```
-k self-signed                          -> 18 0 (with %{proxy_ssl_verify_result}), exit 0
--cacert cert.pem                       -> 0, exit 0
no option                               -> 18, exit 60
-k expired self-signed                  -> 18, exit 0
expired self-signed, --cacert for it    -> 10, exit 60
expired self-signed, no option          -> 18, exit 60
expired leaf + CA sent, -k / none       -> 20 / 20 (exit 0 / 60); --cacert ca.pem -> 10, exit 60
valid leaf + untrusted CA sent, -k/none -> 20 / 20
leaf only, issuer missing, -k/none      -> 20 / 20
--cacert ca.pem https://127.0.0.1 (no IP SAN)          -> 1, exit 60
--cacert ca.pem --resolve other:...:127.0.0.1 https://other -> 1, exit 60 ("SSL: no alternative certificate subject name matches target hostname 'other'")
same with -k                            -> 20, exit 0
--proxy-insecure -x https://localhost:18443 http://example.invalid/ -> "0 18", exit 0
--proxy-cacert cert.pem -x ...          -> 0 (proxy), exit 0
-x https://... without --proxy-insecure -> 18 (proxy), exit 60
-w '%{json}' -k                         -> "proxy_ssl_verify_result":0, "ssl_verify_result":18
http:// URL                             -> 0, exit 52
```

### Decisions (ADR-0282)

- The code travels as a new default-no-op event, `ITransferEvents.ReportCertificateVerifyResult(long, bool isProxy)`,
  because only an event reaches the runner from a *failed* handshake (exit 60 still prints 18);
  `TlsHandshakeEvent` is reported only on success, and a `ConnectResult`/`TransferReport` member
  would have needed `Curl.Protocol.Http` (outside `touches`) to copy it.
- Networking reports it only in the OpenSSL build, so Windows keeps 0 (ADR-0043).
- Measurement showed `OfChainStatus` had the order wrong (date before trust; BL-404's guess).
  Now trust (18/19/20) beats date (9/10), matching BL-150's `-v` measurement too.
- A host name refused without `-k` reports 1 (`X509_V_ERR_UNSPECIFIED`), as measured.
- The Console records codes only when the group has `-w` (`VerifyResultRecordingTransferEvents`);
  otherwise the handler's `Events` stays the same object, which many `ConnectTarget` equality
  tests rely on. Two HTTP/3 runner tests that do use `-w` now compare the target `with { Events = NoTransferEvents.Instance }`.
- `Documentation/Planning/Decisions` (ADR-0282 and the index row) was edited outside `touches`;
  no task in Doing names it.

### Left for later (noted in ADR-0282)

FTPS data-connection events (`FtpDataConnectEvents` in `Curl.Protocol.Ftp`) do not forward the
new event; `--crlfile` refusals report the chain's code rather than the CRL code; a QUIC
verification refusal reports no code. None is measured yet.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. %{ssl_verify_result} and %{proxy_ssl_verify_result} print the OpenSSL verify code off Windows (18 self-signed, 0 trusted, 20 untrusted issuer, 1 name refused), 0 on Windows
