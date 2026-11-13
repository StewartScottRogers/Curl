---
id: BL-5112
title: Show the value placeholder for --require-sct and other value options in --extended-help
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs, Curl.Extension.Composition.UnitTests/ExtensionListingTests.cs, Curl.Extension.CertificateTransparency.UnitLibrary/CertificateTransparencyExtension.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5112 — Show the value placeholder for --require-sct and other value options in --extended-help

## Goal

`curl --extended-help` shows `--require-sct` with the value it consumes (for example `--require-sct <log-list>`), so a user can tell it takes an argument.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-061 (Low, Confirmed; help text). Part B: judged against the port's own docs and specs, not curl.

**Repro.** `curl --extended-help | grep -n -A3 -i "require-sct"`, then `curl -sS --require-sct https://example.com/`

**Expected.** The help shows the argument the option consumes (a v3 JSON log-list file path), as it does for other value-taking extension options, for example `--require-sct <log-list>`.

**Actual.** The extension's options line reads `Options: --require-sct` with no placeholder; the second command treats the URL as the log-list path and prints `curl: (2) no URL specified`.

**Where to start.** `--require-sct` is declared `ExtensionOptionArgument.Value` in `Curl.Extension.CertificateTransparency.UnitLibrary/CertificateTransparencyExtension.cs`. The one-line listing in `Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs` (`LineOptionText`) prints `"--" + option.Name` for every value option, so the placeholder is missing for all of them, not just this one (the long form, `OptionText`, prints a generic `<value>`). `ExtensionOption` (`Curl.Extension.Abstractions.UnitLibrary/ExtensionOption.cs`) has no placeholder field. The review's own acceptance (`--require-sct <file>` "or similar") allows the generic `<value>` in the listing; a named placeholder is better if it can be added without touching the many extensions (for example an optional placeholder derived from the option's help text, or a new optional field with a default), and that wider change goes in `touches` first. Also check `curl --help all` and the `--ai-help` text for the same option.

## Acceptance criteria

- [ ] Tests added to `Curl.Extension.Composition.UnitTests/ExtensionListingTests.cs` prove the one-line listing prints a placeholder after every value-taking option (`--require-sct <...>`) and prints a boolean option (`--name, --no-name`) unchanged, and the long form agrees with the one-line form.
- [ ] A test in `Curl.Extension.CertificateTransparency.UnitTests/CertificateTransparencyExtensionTests.cs` proves the `--require-sct` description shows the placeholder naming the log-list file.
- [ ] `curl --help all` and `--ai-help` agree with `--extended-help` for `--require-sct`, or the criterion's test shows they do not list it; the check is by a named test or a stated grep.
- [ ] Tests are platform-neutral.
- [ ] `dotnet build Curl.Extension.Composition.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. No server is needed here.
- Extension isolation holds: the extension references only `Curl.Extension.Abstractions` and shared layers.
- Review acceptance check, as a hand check: `curl --extended-help` shows `--require-sct <file>` (or similar), and `curl --help all` agrees.

## Log

- 2026-11-13: Created.
