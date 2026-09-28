---
id: BL-642
title: Parse --doh-url, --doh-insecure and --doh-cert-status and resolve through DoH
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-641]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-642 — Parse --doh-url, --doh-insecure and --doh-cert-status and resolve through DoH

## Goal

`--doh-url <url>` makes every transfer resolve through `DohDnsResolver` (BL-641), `--doh-insecure` skips the DoH server's certificate check, `--doh-cert-status` behaves as the platform's curl 8.21.0 build does (measured; see BL-610 for `--cert-status`), and `-v` writes curl's DoH lines.

## Context

- Conformance audit 2026-09-28, row 27. Resolver: BL-641; design: BL-639's ADR.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; compose the resolver in `Curl.Console/CurlTransports.cs`. `--resolve` entries still win over DoH if curl's do (measure).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k` as the DoH server: `-v --doh-url ... --doh-insecure http://example.test:<P>/` with an A answer pointing at a second recorder, `--doh-url` with `--resolve` for the same host, `--doh-url bogus`, and `--doh-cert-status`; stderr, exit code and both recorders' requests copied into Notes.
- [ ] `Curl.Cli.UnitTests` cover parsing and refusals; `Curl.Console.UnitTests` pin each measured case through fake connectors.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
