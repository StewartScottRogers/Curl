# ADR-0623 — The SSH key callbacks decide the host key inside the SSH handler

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2422.

## Context

`Curl.Api`'s `EasyCallbackOptions` carries `CURLOPT_SSH_KEYFUNCTION` (`SshKeyCallback`, given the known key, the found
key and an `SshKeyMatch`, answering an `SshKeyStatus`), `CURLOPT_SSH_HOSTKEYFUNCTION` (`SshHostKeyCallback`, given the
key type and bytes, answering an `SshKeyMatch`) and `CURLOPT_SSL_CTX_FUNCTION` (`SslContextCallback`). Nothing called
them, and `Libcurl` answered `CURLE_NOT_BUILT_IN` for all three and their `*DATA` options.

The host key is checked inside `Curl.Original.Protocol.Ssh`'s `SshHostKeyChecker`, which sees only the transfer's
`SshOptions` (in `Curl.Protocol.Abstractions`, which cannot reference `Curl.Api`). A library handle's perform passed no
`SshOptions` at all (`ManagedEasyTransfer.OwnInputsOf`), so `CURLOPT_SSH_KNOWNHOSTS`, the fingerprints and the key
files never reached the handler either.

In curl 8.22.0's `lib/vssh/libssh2.c`, `ssh_check_fingerprint` checks `CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256` and `_MD5`
when given and returns; otherwise, with `CURLOPT_SSH_HOSTKEYFUNCTION` set it calls it with the key's
`convert_ssh2_keytype` type and bytes and fails with `CURLE_PEER_FAILED_VERIFICATION` for any answer but
`CURLKHMATCH_OK`, else it calls `ssh_knownhost`. That does nothing without `CURLOPT_SSH_KNOWNHOSTS`; with it, it looks
the key up (`libssh2_knownhost_checkp`), casts the `LIBSSH2_KNOWNHOST_CHECK_*` result to `enum curl_khmatch`, passes the
entry's key (its base64 text, `len` 0) when the check is `MATCH` or `MISMATCH`, and asks `CURLOPT_SSH_KEYFUNCTION`, or
without one the default `sshkeycallback`, which answers `CURLKHSTAT_FINE` for a match and `CURLKHSTAT_REJECT`
otherwise. `REJECT`, `DEFER` and any unknown answer fail with `CURLE_PEER_FAILED_VERIFICATION`; the three `FINE*`
answers accept, `FINE_ADD_TO_FILE` and `FINE_REPLACE` also writing the known-hosts file. `CURLOPT_SSL_CTX_FUNCTION`
and `_DATA` answer `CURLE_NOT_BUILT_IN` in `setopt.c` unless the TLS backend supports `SSLSUPP_SSL_CTX`, which Schannel
does not.

## Decision

1. **Two hooks on `SshOptions`.** `HostKeyAcceptance` (`Func<string, ReadOnlyMemory<byte>, bool>`, the key type
   name and blob) and `KnownHostDecision` (`Func<SshKnownHostQuery, SshKnownHostVerdict>`), with the new
   `SshKnownHostMatch` and `SshKnownHostVerdict` enums numbered as `curl_khmatch` and `curl_khstat`, so the engine maps
   them by number as curl casts them. The abstractions stay free of `Curl.Api`.
2. **`SshHostKeyChecker` asks them where libcurl does.** With no fingerprint, `HostKeyAcceptance` replaces the
   known-hosts check (and is asked under `-k` too); otherwise the known-hosts check asks `KnownHostDecision`, or applies
   the default rule, after printing `SSH: host check`. A refusal prints `SSH: knownhost check failed` and fails with
   exit 60 and `SSL peer certificate or SSH remote key was not OK`; `SSH: knownhost entry matches host key` is printed
   only when the key matched. A host key of a type the known-hosts file cannot compare is still refused before the
   callback, as before.
3. **The engine adapts the callbacks.** `CallbackSshHostKeys` maps the key type name to `SshKeyType` (`ssh-rsa`,
   `ssh-dss`, any `ecdsa-sha2-*`, `ssh-ed25519`, else `Unknown`), uses the found key's type for the known key too, and
   gives the known key as its base64 text in ASCII bytes, as libssh2 holds it. `SshOptionsMapping.OfHandle` builds a
   library handle's `SshOptions` from `EasySshOptions` (the known-hosts file only when `CURLOPT_SSH_KNOWNHOSTS` names
   one; libcurl looks for no default) and those hooks, and `OwnInputsOf` gives it to every `scp` or `sftp` perform
   without `ToolInputs`. The curl tool's runner keeps choosing its own `SshOptions`.
4. **`Libcurl/Callbacks/Keys`** sets the two SSH functions (their delegate or `null`, else `BadFunctionArgument`),
   takes any `*DATA` value and changes nothing, and answers `NotBuiltIn` for `CURLOPT_SSL_CTX_FUNCTION` and `_DATA`
   whatever the value, as the Schannel build does.
5. **The known-hosts file is not written yet.** `FINE_ADD_TO_FILE` and `FINE_REPLACE` accept the key as `FINE` does;
   writing the file is BL-2489.

## Consequences

A library handle's `scp` and `sftp` transfers now honour `CURLOPT_SSH_KNOWNHOSTS`, the host key fingerprints, the key
files and `CURLOPT_KEYPASSWD`, which they silently ignored before. The curl tool sets no callback, so its behaviour is
unchanged.
