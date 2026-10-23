# ADR-0785: Differential case files give bytes as base64 or a repeated string, and are embedded by path

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3259
- Decided by Claude under Stewart's delegation.

## Context

ADR-0782's case file gives `stdin` and each `files` value only as UTF-8 text. The tester's
`cases.py` and `cases_more.py` hold 23 cases whose content is a Python `b"..."` literal: bytes
that are not UTF-8 text, and `b"x" * 2_000_000` for `post-big-expect`, which a JSON file should
not spell out. The case files also have to reach the host and the tests without a path on disk.

## Decision

`stdin` and every `files` value is one of three shapes, read by `DifferentialCaseFileReader`:
a JSON string (UTF-8 text, as before), `{"base64": "..."}` (the decoded bytes), or
`{"repeat": "x", "count": 2000000}` (the UTF-8 bytes of `repeat`, `count` times). The same shapes
serve both properties rather than a separate `stdinBase64` property, so one rule covers every
place content appears.

Every `Cases/**/*.json` in `Curl.Conformance.Differential.UnitLibrary` is an embedded resource
with the SDK's default manifest name; `DifferentialCaseFileReader.ReadEmbedded("CliHttp/X.json")`
maps the path to `Curl.Conformance.Differential.Cases.CliHttp.X.json` and reads it, throwing
`FileNotFoundException` for a name that is not embedded. `Cases/Smoke/SmokeCases.json` holds one
case, a plain GET, and pins the embedding.

## Why

Base64 is the BCL's own byte encoding (`Convert.FromBase64String`) and keeps the file JSON. A
repeat count keeps a 2 MB body a one-line case, as it was in the tester's Python. The default
manifest name is the same on every platform, where a `LogicalName` built from `RecursiveDir`
would carry `\` on Windows and `/` elsewhere. `GetManifestResourceStream` needs no reflection, so
the library stays AOT-compatible.
