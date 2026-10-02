---
id: BL-1106
title: Write the --libcurl setopt lines for the FTP, TFTP, telnet, mail, MQTT, retry, rate and remaining options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-654]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1106 — Write the --libcurl setopt lines for the FTP, TFTP, telnet, mail, MQTT, retry, rate and remaining options

## Goal

The `--libcurl` generator writes curl 8.21.0s lines for every option `LibcurlSourceCodeOptionCoverageTests` lists under `WaitingForBl1106` (FTP, TFTP, telnet, mail, MQTT, retry, rate, range, upload, DNS, interface, HTTP version and the rest), so that list is empty.

## Context

- Split from BL-654, which wrote the TLS, proxy and authentication lines (ADR-0326).
- Measure each option with `Record-CurlExchange.ps1 -NoServer -CurlArgs --libcurl,-,-s,...` as in BL-654 Notes; move each option to `Written` or `WritesNothing` as it lands, and delete the empty list.
- `-G -d a=1` URL handling was left by BL-653.

## Acceptance criteria

- [ ] Measured first; output copied into Notes.
- [ ] `Curl.Cli.UnitTests` reproduce each measured output byte for byte, and `WaitingForBl1106` is gone with the enumeration test passing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
