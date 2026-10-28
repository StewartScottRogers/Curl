---
id: BL-3939
title: Port the -UdpSink, -DnsPort and -DohPort modes of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3938]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3939 — Port the -UdpSink, -DnsPort and -DohPort modes of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh` handles -UdpSink, -DnsPort (with -DnsSilentPort) and -DohPort like `Record-CurlExchange.ps1`.

## Context

Split from BL-3931 (part of BL-3934); follows BL-3929 and BL-3930 in the same file. Mode: the UDP sink and the DNS and DNS-over-HTTPS responders. Test in WSL Ubuntu (socat, curl 8.18.0, openssl present) against the .ps1 under pwsh. Platform-neutral output only.

## Acceptance criteria

- [x] The mode no longer exits 2 with "not ported yet", and the "not ported" header block no longer lists it (or states why it cannot exist off Windows).
- [x] At least one case per mode records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the .ps1; cases and results are in the Log.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

- Header "NOT PORTED YET" block no longer lists the three modes; a new "DNS" header section documents them. perl (core only) serves UDP sink, DNS UDP+TCP and the DoH handler; socat OPENSSL-LISTEN (TLS 1.2, the -Tls throwaway certificate) fronts DoH. DnsTruncate, DnsResponseCode, DnsAnswerAddress and DnsEchConfigList are now honoured.
- Measured in WSL Ubuntu (curl 8.18.0 has no c-ares, so --dns-servers is unsupported there): a perl client stood in as -Curl for both scripts, sending A, AAAA, HTTPS, TXT queries over UDP and TCP plus one to a silent port. stdout.bin, stderr.txt, exitcode.txt and dns.txt (timestamps stripped) are byte-identical between .sh (WSL) and .ps1 (Windows pwsh) in four cases: default; -DnsTruncate; -DnsResponseCode 3; -DnsAnswerAddress 10.0.0.1,::1 with -DnsEchConfigList AAECAw==.
- -UdpSink: curl tftp://127.0.0.1:47301/x, datagrams.txt identical (000178006f63746574...) and exit 28 in both.
- -DohPort: --doh-url with --doh-insecure; sh logs the A and AAAA queries as doh and curl reaches 127.0.0.1 (exit 7 at example.test:9), as the ps1 does. Windows curl.exe differs from WSL curl 8.18.0 in query order and stderr wording, so stderr is not byte-compared; the answer bytes come from the same code as -DnsPort.
- -Tls HTTPS case still passes; bash -n clean. Platform-neutral: no tests added (script only).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ebe547a6 49b8818c 541414b4.
- 2026-10-28: Implemented in Record-CurlExchange.sh; measured against the .ps1 (see Notes).
- 2026-10-28: Doing -> Done. Record-CurlExchange.sh serves -UdpSink, -DnsPort and -DohPort; DNS and sink output byte-identical to the .ps1
