---
id: BL-353
title: Parse --ipfs-gateway in Curl.Cli with curl's option-time checks
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-353 — Parse --ipfs-gateway in Curl.Cli with curl's option-time checks

## Goal

`--ipfs-gateway <url>` is parsed into the options model, and the text curl 8.21.0 rejects while parsing options is rejected with curl's exit code and message before any transfer.

## Context

- Found by BL-210, which added `Curl.Core.IpfsGatewayRewriter`: it takes the `--ipfs-gateway` text as a `string?`, but `Curl.Cli.UnitLibrary` has no such option yet. BL-240 wires the rewriter into `Curl.Console` and needs the parsed value.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27, with `ipfs://cid` as the URL:
  - `--ipfs-gateway ""`: `curl: option --ipfs-gateway: blank argument where content is expected`, then curl's `try 'curl --help'` line, exit 2.
  - `--ipfs-gateway :::` and `--ipfs-gateway foo://h:1/`: `curl: --ipfs-gateway was given a malformed URL`, then the `try 'curl --help'` line, exit 43.
  - `--ipfs-gateway 127.0.0.1:1` is accepted (a gateway with no scheme is read as `http` by the rewriter).
- https://curl.se/docs/manpage.html#--ipfs-gateway

## Acceptance criteria

- [ ] `--ipfs-gateway <url>` sets the gateway on the parsed options, the last one given winning; a test pins it.
- [ ] The blank and malformed cases above produce the measured stderr bytes and exit codes, each pinned in a test.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

## Log

- 2026-09-27: Created.
