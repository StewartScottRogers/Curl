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
completed: 2026-10-14
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

- [x] `UpstreamPerlSubstitution` interprets 1287's strip program (1286's moved to BL-2238) exactly as Perl would on the sample lines, pinned by `UpstreamPerlSubstitutionTests` with matching and non-matching lines.
- [x] `UpstreamCommandLineSplitter` and the runner carry out `>` and `>>` redirects of stdout into a file under the log directory (a redirect outside it is still refused); tests in `UpstreamCommandLineSplitterTests`.
- [x] `<client><connectport>`: split off to BL-2239 (see Notes).
- [x] Cases 1287 and 321 are no longer screened out and pass (1286 and 1316 moved to BL-2238 and BL-2239). Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [x] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

- Split on the first pass (2026-10-14, lane 1): four separate harness features would not fit one run's cost cap, so 1286's Digest-sorting strip program went to BL-2238 and 1316's `<connectport>` to BL-2239; this task kept the `>`/`>>` redirect (321) and 1287's `or $_ = ''` strip.
- Redirect: one `> file` or `>> file` that starts a word is the stdout redirect; a second or mid-word `>` (`2>`), or one with no file, is still unsupported. runtests.pl appends its own `>%LOGDIR/stdoutN` unless the option says `no-stdout`, and the shell's last redirect of a stream wins, so without `no-stdout` the case's file is opened (created or appended) but gets nothing. A redirect outside the log directory is skipped by screening, like a file part.
- `s/.../.../ or $_ = ''` (and `""`): the line is emptied when the pattern does not match; `Strip` then drops it, as runtests.pl drops empty lines.
- Measured: 321 and 1287 pass and are listed; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. The upstream case runner carries out stdout > and >> redirects and the s/// or $_ = '' strip form; 321 and 1287 pass; 1286 and 1316 split to BL-2238 and BL-2239
