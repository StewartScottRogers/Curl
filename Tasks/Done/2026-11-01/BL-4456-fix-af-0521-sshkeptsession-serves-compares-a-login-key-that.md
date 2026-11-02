---
id: BL-4456
title: Fix AF-0521: SshKeptSession.Serves compares a login key that holds the password and pass phrase with string ==
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4456 — Fix AF-0521: SshKeptSession.Serves compares a login key that holds the password and pass phrase with string ==

## Goal

The defect the audit office reported as AF-0521 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0521 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0521-sshkeptsession-serves-compares-a-login-key-that-ho.md`.

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs:41`

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs:41`

Line 36 builds the login key as string.Join('\0', UserName, Password, PrivateKeyPath, PublicKeyPath, PrivateKeyPassphrase). Line 41, `internal bool Serves(string key) => key == loginKey;`, compares two of these keys with ordinal string equality, which stops at the first differing character. The password and pass phrase are secrets, so the comparison is not constant time, as the method requires for any secret. Upstream curl compares the credentials for connection reuse with Curl_timestrcmp. Exploitability is low: both keys come from the user's own command line or config (the session's login and the next URL's login). No remote party controls either value, so I found no attacker-observable oracle. Reported at the rubric's severity for a non-constant-time secret comparison.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs -Pattern 'key == loginKey'
```

- Expected: No match: the keys (or the password and pass phrase within them) are compared with CryptographicOperations.FixedTimeEquals over their bytes.
- Actual: Curl.Original.Protocol.Ssh.UnitLibrary\SshKeptSession.cs:41:    internal bool Serves(string key) => key == loginKey;

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decision: `Serves` compares the SHA-256 digests of both keys' UTF-8 bytes with `CryptographicOperations.FixedTimeEquals`, not `FixedTimeEquals` over the raw bytes. `FixedTimeEquals` returns at once when the lengths differ, so raw bytes would reveal the key's length. Digests are always 32 bytes. This matches the aim of curl's `Curl_timestrcmp`: no early exit on the secret's content.
- The reproduction now gives no match. `SshKeptSessionTests` pins an equal key, keys whose content differs and keys whose length differs.
- Measure-CodeQuality for Curl.Original.Protocol.Ssh.UnitLibrary: 0 failing members. Full build clean, fast tests green on Windows. The change is platform-neutral (no path, OS error or certificate).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Done. Serves compares the login key in constant time
