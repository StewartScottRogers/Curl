---
id: BL-6015
title: Fix fitness finding STD-221: --doh-url cannot use a DoH server that only speaks HTTP/2; curl resolves through it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6015 — Fix fitness finding STD-221: --doh-url cannot use a DoH server that only speaks HTTP/2; curl resolves through it

## Goal

STD-221 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: --doh-url cannot use a DoH server that only speaks HTTP/2; curl resolves through it. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (DoH is broken against HTTP/2-only servers such as Unbound and dnsdist) · **Confidence:** Confirmed (2 runs by the orchestrator against Unbound; also seen against dnsdist on Windows and Linux) · **Area:** DNS over HTTPS, Linux build (the Windows build fails the same way; Windows REF has no HTTP/2, so only Linux is compared).
- **Server:** Unbound with DoH on port 443 and `local-data: "www.test.local. 3600 IN A 192.0.2.20"`, self-signed certificate: `docker run -d --name unbound -p 127.0.0.1:29953:443/tcp -v <unbound.conf>:/etc/unbound/unbound.conf:ro -v <certs>:/certs:ro klutchell/unbound:1.26.2`. Its DoH answers HTTP/2 only (an `--http1.1` POST gets no response).
- **Repro (WSL):** `curl -sv -m 5 -o /dev/null --doh-url https://127.0.0.1:29953/dns-query --doh-insecure http://www.test.local:1/`
- **Expected (REF2 8.18):** `Host www.test.local:1 was resolved.`, `IPv4: 192.0.2.20`, then `Trying 192.0.2.20:1...` (exit 28 from the unreachable target).
- **Actual (.NET Linux):** `* Could not resolve host: www.test.local`, exit 6. The Windows build also exits 6. Against a DoH server that also speaks HTTP/1.1 (adguard dnsproxy), the port resolves.
- **Likely location:** the DoH resolver's HTTP client in the encrypted-DNS / DoH path (`Curl.Extension.EncryptedDns` or the `--doh-url` resolver) sends HTTP/1.1 only and never negotiates `h2` by ALPN.
- **Acceptance check:** the WSL repro with the Linux build prints `Trying 192.0.2.20:1` (exit 28, not 6).

## Acceptance criteria

- [ ] the WSL repro with the Linux build prints `Trying 192.0.2.20:1` (exit 28, not 6).
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
