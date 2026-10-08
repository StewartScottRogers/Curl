---
id: BL-1724
title: Measure the URL scheme and curl -V feature gaps with Gap/Tools/Measure-VersionGap.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721, BL-1722]
touches: [Gap/Tools/Measure-VersionGap.ps1, Gap/Tools/Fixtures/version, Gap/Upstream/8.21.0/protocols.json, Gap/Upstream/8.21.0/features.json]
requirement: none
created: 2026-10-08
completed:
---
# BL-1724 — Measure the URL scheme and curl -V feature gaps with Gap/Tools/Measure-VersionGap.ps1

## Goal

`Gap/Tools/Measure-VersionGap.ps1` writes two area measurements, `protocols` and `features`.
It compares Curl's `curl -V` and per-scheme behaviour with the release's documents and the
matched reference build's `curl -V`.

## Context

This is ADR-0433 decision 2, areas `protocols` and `features`. Formats are in
`Gap/Instructions/Gap-Format.md` (BL-1720). Use `Get-UpstreamRelease.ps1` (BL-1721) and
`Invoke-GapProbe.ps1` (BL-1722).

**Protocols inventory.** `docs/cmdline-opts/_PROTOCOLS.md` has one `##` heading per
protocol, and an `(S)` suffix marks the TLS variant. In 8.21.0 that is 17 headings covering
26 schemes: DICT, FILE, FTP(S), GOPHER(S), HTTP(S), IMAP(S), LDAP(S), MQTT, POP3(S), RTSP,
SCP, SFTP, SMB(S), SMTP(S), TELNET, TFTP and WS(S). Checked against the curl-8_21_0 tag on
2026-10-08. The committed inventory holds only what the document names, so it does not
depend on the platform. When measuring, add every scheme on the matched reference build's
`curl -V` `Protocols:` line that the document lacks (for example `ipfs` and `ipns`) as an
item of that run. Key: `protocols:<scheme>` in lower case.

**Features inventory.** `docs/cmdline-opts/version.md` has one `##` heading per feature,
the name in backticks (for example `` ## `alt-svc` ``, `` ## `HTTP2` ``). As with schemes,
the inventory is the document's list. When measuring, add the names on the reference's
`Features:` line that the document lacks. Key: `features:<name>` as upstream spells it.

**Measurement.**

- A scheme is `match` when Curl's `Protocols:` line and the reference's agree on whether
  they list it, and a probe of `<scheme>://127.0.0.1:1/` (port 1, nothing listening) gives
  the same exit code through both binaries. A scheme the reference build does not list is
  `excluded` with reason `reference-lacks:<scheme>` when Curl does not list it either. It
  is `gap` when Curl lists it, because Curl matches the build it targets (ADR-0021, ADR-0397).
- A feature is `match` when both `Features:` lines agree on it. A feature the reference
  lacks and Curl also lacks is `excluded` with `reference-lacks:<name>`.
- Without a matching reference, fall back to the documents, so every documented scheme and
  feature is expected. Record `referenceFallback: "docs"`.

**Parameters.** `-UpstreamRoot`, `-Version`, `-Candidate`, `-OutDirectory` (writes
`protocols.json` and `features.json`), `-InventoryOnly` (writes both inventories to
`Gap/Upstream/<version>/`, with no probing and no Windows-only API; BL-1735 runs it on
Linux), `-SelfTest` (fixtures under `Gap/Tools/Fixtures/version/`: a fake `_PROTOCOLS.md`,
a fake `version.md`, and canned `curl -V` texts for both binaries, through a
`-ProbeResults` file).

Commit both 8.21.0 inventories.

## Acceptance criteria

- [ ] `Gap/Tools/Measure-VersionGap.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: `(S)` headings give both schemes; a reference-only scheme joins the measurement but not the inventory; agreement gives `match`; a scheme Curl lists that the reference lacks gives `gap`; a scheme both lack gives `excluded` with `reference-lacks:`; the docs fallback expects every documented item.
- [ ] `Gap/Upstream/8.21.0/protocols.json` (at least the 26 documented schemes) and `Gap/Upstream/8.21.0/features.json` are committed and valid against `Gap-Format.md`.
- [ ] A real run on Windows writes both measurements, and their counts are recorded in this task's Notes.
- [ ] `-InventoryOnly` runs under `pwsh` with no binaries present. The header help documents every parameter. The script is ASCII only.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
