---
id: BL-2223
title: Run the Perl strip lines, shell redirects and <connectport> that cases 1286, 1287, 321 and 1316 need in the upstream case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2223 — Run the Perl strip lines, shell redirects and <connectport> that cases 1286, 1287, 321 and 1316 need in the upstream case runner

## Goal

Make the upstream case runner carry out the two Perl `<strip>` programs, the `>>` shell redirect and the `<client><connectport>` part that keep cases 1286, 1287, 321 and 1316 unmeasured.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had 38 `harness-unsupported` cases, and running `Curl.Conformance.UnitTests` on 2026-10-14 names these four. The gap tool uses `Curl.Conformance.UnitLibrary`.

- 1286: strip code `if(s/^(Authorization: Digest )([^\r\n]+)(\r?\n)$//) { $_ = $1 . join(', ', map { s/^(cnonce=)"[a-zA-Z0-9+\/=]+"$/$1REMOVED/; s/^(response=)"[a-f0-9]{32}"$/$1REMOVED/; s/^qop="auth"$/qop=auth/; $_ } sort split(/, */, $2)) . $3; }` - `UpstreamPerlSubstitution.Parse` returns null for it (screening: `UnsupportedStripCode`).
- 1287: strip code `s/^.*(?=\* Ignoring (?:Content-Length|Transfer-Encoding) )// or $_ = ''` - a `s///` with a lookahead followed by `or $_ = ''`.
- 321: `<command>` ends `--etag-save - >> %LOGDIR/out%TESTNUMBER` (screening: `UpstreamCommandLineSplitter` reports `UnsupportedShellSyntax` for `>`); support `>` and `>>` of stdout to a file inside the log directory, as the shell does.
- 1316 (ftp + http-proxy): `<client><connectport>` (not in `ClientParts`); read what `runtests.pl` at curl-8_22_0 does with it (it names the port the proxy is expected to connect to) and verify it against the proxy stand-in's recorded CONNECT target.

Prefer the narrowest, general implementation: a Perl construct per case is acceptable only when the case needs it; each must be a faithful interpretation (no `eval`, no process spawn; AOT-safe).

## Acceptance criteria

- [ ] `UpstreamPerlSubstitution` (or its sibling) interprets 1286's and 1287's strip programs exactly as Perl would on the sample lines, pinned by `UpstreamPerlSubstitutionTests` with matching and non-matching lines.
- [ ] `UpstreamCommandLineSplitter` and the runner carry out `>` and `>>` redirects of stdout into a file under the log directory (a redirect outside it is still refused); tests in `UpstreamCommandLineSplitterTests`.
- [ ] `<client><connectport>` is acted on as in `runtests.pl`; a test pins it.
- [ ] Cases 1286, 1287, 321 and 1316 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

## Log

- 2026-10-14: Created.
