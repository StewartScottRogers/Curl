---
id: BL-486
title: Call TcpConnector.LoadResolveEntries at the start of every transfer in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-482]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-486 — Call TcpConnector.LoadResolveEntries at the start of every transfer in Curl.Console

## Goal

With `-v` and `--resolve`, every transfer on the command line prints curl 8.21.0's `Added H:P:A to DNS cache` lines first (with `RESOLVE H:P - old addresses discarded` before each from the second transfer on), not only the first.

## Context

- BL-482 (ADR-0114) added `TcpConnector.LoadResolveEntries(ITransferEvents)`. Without a call, `TcpConnector.ConnectAsync` loads the entries itself once, on the first connect, so only the first transfer prints the lines today. The console shares one `ITransferEvents` across the run (`TransferEventOutput`), so the connector cannot tell transfers apart by itself.
- Measured 2026-09-27 with curl 8.21.0 (mingw Schannel), `Record-CurlExchange.ps1`, `-s -v`, each response `Connection: close`:
  - `--resolve foo.example:P:127.0.0.1 http://foo.example:P/a http://foo.example:P/b`: the second transfer starts `RESOLVE foo.example:P - old addresses discarded`, `Added foo.example:P:127.0.0.1 to DNS cache`, right after `shutting down connection #0`.
  - `-L`, a 302 to the same host on a fresh connection: no `Added` line for the redirect; only a new URL (transfer) reloads.
- The runner builds the connector in `CurlComposition.CreateTcpConnector`; call the method once per URL transfer (and per `--retry` attempt if curl reloads there: measure first), before any other line of that transfer, not per redirect hop.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test pins, for two URLs with one `--resolve` entry and `-v`, the second transfer's `RESOLVE ... - old addresses discarded` and `Added ... to DNS cache` lines before its `Hostname ... was found in DNS cache` line.
- [x] A test pins that a followed redirect (`-L`) prints no second `Added` line.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-482 (2026-09-27): `Curl.Console` was held by BL-458 in another lane.
- Measured 2026-09-27, curl 8.21.0 (mingw Schannel), `Record-CurlExchange.ps1`:
  `-s -v --retry 1 --retry-delay 1 --resolve foo.example:P:127.0.0.1 http://foo.example:P/a`
  with a `503` then a `200` (both `Connection: close`): one `Added` line only; the retried
  attempt starts `Hostname foo.example was found in DNS cache` right after
  `shutting down connection #0`. So `--retry` does not reload the entries; only a new URL
  (glob match) transfer does.
- Done: `TransferDispatch` takes an optional `loadResolveEntries` delegate
  (`TransferDispatch.LoadResolveEntries(ITransferEvents)`, a no-op without one);
  `CurlComposition.CreateTransferDispatch` passes `transports.TcpConnector.LoadResolveEntries`;
  `CurlCommandRunner.TransferUrlAsync` calls it once per transfer just before the `-b` cookie
  files load (curl's `Curl_pretransfer` order), after the IPFS and `-T` URL checks, which
  fail before curl's perform. Not inside the retry or redirect loops.
- No ADR: the behaviour is measured, not chosen. The feature pipeline was run in-session
  (the change is three lines of production code); tests in
  `CurlCommandRunnerResolveEntryTests` run the production handler set over a real
  `TcpConnector` on `ScriptedTcpDialer`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. With -v and --resolve every URL's transfer prints the Added ... to DNS cache lines (RESOLVE ... old addresses discarded from the second on); retries and redirects reload nothing
