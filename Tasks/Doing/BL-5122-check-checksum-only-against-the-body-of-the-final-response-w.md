---
id: BL-5122
title: Check --checksum only against the body of the final response when -L follows redirects (EXT-049)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Checksum.UnitLibrary/ChecksumTransferHook.cs, Curl.Extension.Checksum.UnitTests/ChecksumRedirectTests.cs, Documentation/Planning/Decisions/ADR-1096-checksum-verifies-the-response-body-against-a-fips-180-4-digest-and-fails-with-exit-61.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5122 — Check --checksum only against the body of the final response when -L follows redirects (EXT-049)

## Goal

`--checksum` with `-L` hashes and checks only the response body curl writes to its output (the final response), so a correct download behind a redirect succeeds and a wrong one fails with exit 61 naming the final body's digest.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-049 (Medium, Confirmed). No checksum can succeed through a redirect, for example a GitHub release download; integrity is not bypassed, only wrongly refused.

**Repro.** With a server where `/redir` answers `302 Location: /plain` with an empty body and `/plain` answers `hello\n` (write it in PowerShell or a C# file-based app, never Python):
1. `curl -sS -m 5 --checksum sha256:5891b5b522d5df086d0ff0b110fbd9d21bb4fc7163af34d08286a2e846f6be03 -L http://127.0.0.1:28000/redir -o o.txt` (SHA-256 of `hello\n`)
2. Control: the same with `--checksum sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` (SHA-256 of the empty string).

**Expected.** ADR-1096 "What is hashed: the response body as curl writes it to its output". With `-L` only the final body is written, so step 1 exits 0 with `hello` in `o.txt`.

**Actual.** Step 1 prints `curl: (61) checksum mismatch: expected sha256:5891..., got sha256:e3b0c442...` (the empty 302 body's hash), exits 61, and `o.txt` is not created. Step 2 passes the first hop, then fails the second with 61.

**Where to start.** `Curl.Extension.Checksum.UnitLibrary/ChecksumTransferHook.cs` runs once per redirect hop (the per-hop invocation of ADR-1135 point 5, the transfer-hook chain) and judges the 3xx body. Skip hops that curl follows (the response is a redirect, `context.Http.FollowRedirects` is set and the hop is followed), or hash only the final response. Find how the per-hop `next` result reports a followed redirect (`TransferResult` / `TransferReport`) by reading the engine's redirect loop; a 3xx that is not followed (no `-L`, or `--max-redirs` reached) is the output and is still checked. Record the decision in ADR-1096. The review also suspects `--sigstore` with `-L` has the same per-hop problem (not tested); check `Curl.Extension.Sigstore.UnitLibrary` and file a follow-up task if it does, rather than widening this one.

## Acceptance criteria

- [ ] New tests in `Curl.Extension.Checksum.UnitTests/ChecksumRedirectTests.cs` (new) prove, with a scripted two-hop chain, that with `-L` and the digest of the final body the transfer succeeds (exit 0) and the final body is the output.
- [ ] A test proves that with `-L` and the digest of the empty string (the first hop's body) the transfer fails with `CurlExitCode.BadContentEncoding` (61) and the message `checksum mismatch: expected sha256:e3b0c442..., got sha256:5891b5...` naming the final body's digest.
- [ ] A test proves a 3xx response that is not followed (no `-L`) is still the checked body, and the existing `ChecksumTransferHookTests` stay green.
- [ ] ADR-1096 states that only the final response's body is checked when redirects are followed.
- [ ] The tests are platform-neutral (no real socket).
- [ ] `dotnet build Curl.Extension.Checksum.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: step 1 exits 0 and `o.txt` contains `hello`; step 2 exits 61 with `got sha256:5891b5...`.
- Keep `--ai-help` and `--extended-help` right if their `--checksum` text says anything about redirects.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 66e25795 65e28992 c66235e9 b2f8bcd9 d6cd4299 fd8095de.
