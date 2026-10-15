---
id: BL-2248
title: Resolve through CURL_DNS_SERVER over the composition's datagram connector so the harness can reach a DNS stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2248 — Resolve through CURL_DNS_SERVER over the composition's datagram connector so the harness can reach a DNS stand-in

## Goal

When the environment reader `CurlComposition.CreateRunner` is given holds `CURL_DNS_SERVER=<ip>:<port>`, Curl resolves host names by asking that DNS server over UDP through the composition's injected `IDatagramConnector`, as curl's debug build does (its `override-dns` feature), so the conformance harness's in-memory DNS stand-in (BL-2232) answers them.

## Context

Found by BL-2232 (2026-10-14): upstream cases 2102, 2103 and 2104 set `CURL_DNS_SERVER=127.0.0.1:%DNSPORT` and need the `override-dns` feature. Today no `.cs` file reads `CURL_DNS_SERVER`; `CurlComposition.CreateDnsResolver` (Curl.Console/CurlComposition.cs) builds `DnsServerResolver` only for `--dns-servers` and friends, and `DnsServerResolver` opens its sockets through `IDnsSocketOpener` / `DnsSocketOpener` (`Curl.Networking.UnitLibrary`), not through the run's `IDatagramConnector`. The conformance tests also pass their own `IDnsResolver` (`LoopbackOnlyDnsResolver`) to `CreateRunner`, which would override any resolver chosen from the variable.

In curl 8.22.0, `CURL_DNS_SERVER` is read only by debug builds (`lib/hostip.c` / c-ares setup, `override-dns` in `runtests`); a release build ignores it. Decide (ADR, under Stewart's delegation) whether Curl honours it always or only when the caller asks for the debug behaviour (as the httpsig cases get a fixed clock only from the harness, BL-2183); a release `curl` binary should stay byte-identical to curl's release build, so the likely answer is an opt-in seam on `CreateRunner` (for example a flag or a resolver factory taking the datagram connector) that the harness turns on, and the shipped executable leaves off.

## Acceptance criteria

- [ ] With the seam on and `CURL_DNS_SERVER=127.0.0.1:<port>` in the environment reader, a name is resolved by an A (and, unless `-4`, AAAA) query sent through the injected `IDatagramConnector` to that address and port, and the answer's addresses are dialled; a test in `Curl.Console.UnitTests` pins it with a fake datagram connector.
- [ ] A name the server answers with no address (NXDOMAIN or empty) fails with exit 6 and curl's `Could not resolve host: <name>` text.
- [ ] With the seam off (the shipped `curl`), `CURL_DNS_SERVER` changes nothing; a test pins it.
- [ ] The decision is recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Networking.UnitLibrary` shows no member outside a gate.

## Notes

## Log

- 2026-10-14: Created.
