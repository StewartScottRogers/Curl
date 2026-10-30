# ADR-1144 — --user-from-keychain reads the platform's credential store

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4163.

## Context

BL-4163 adds an extension (ADR-0999), `Curl.Extension.OsCredentialStore.UnitLibrary`, that takes the transfer's user
name and password from the operating system's credential store instead of the command line, where `-u user:password`
leaves the password in the shell history and the process list. curl has no such option. Each platform has its own
store and its own interface, so the extension is tested against each one's documented interface, not against curl.
Without the option nothing changes what curl does.

## Decision

1. **Option and platform.** `--user-from-keychain <name>`, one transfer hook (ADR-1007) registered in the option,
   description and transfer hook catalogues; it has no scheme, so it has no line in `ExtensionCatalogue.cs`. The
   store is the running OS's: Windows Credential Manager on Windows, the keychain on macOS, and libsecret's Secret
   Service on Linux and every other Unix. The credential replaces whatever `-u` gave, for every protocol, because the
   option asks for it by name. No credential under the name fails the transfer with exit 67 (`CURLE_LOGIN_DENIED`)
   and `curl: (67) No credential named <name> in the <store>` before it connects.
2. **Windows Credential Manager** (wincred.h, `CredReadW` and `CREDENTIALW`, Windows 10 and later): the
   `CRED_TYPE_GENERIC` credential whose `TargetName` is the name, `UserName` the user and `CredentialBlob` the
   password, freed with `CredFree`. `cmdkey /generic:<name> /user:<user> /pass:<password>` stores one. The blob has
   no declared encoding: it is read as UTF-16LE when its length is even and it holds a zero byte (as Credential
   Manager, `cmdkey` and callers passing a wide string store it), and otherwise as UTF-8 (as Git Credential Manager
   stores it; UTF-8 text never holds a zero byte). A UTF-16 password with no character below U+0100 and no zero byte
   would read as UTF-8; that is accepted for a rule with no stored type to go on.
3. **macOS keychain** (security(1), macOS 10.13 and later): `/usr/bin/security find-generic-password -g -s <name>`,
   the generic password item whose service is the name. Its `"acct"<blob>=` attribute on standard output is the user
   and `password: ` on standard error the password, each quoted text, `0x` and hexadecimal bytes when not printable
   (decoded as UTF-8), or `<NULL>` for empty. A non-zero exit (44 when no item matches) is no credential. The tool is
   used rather than Security.framework's `SecItemCopyMatching` because a CoreFoundation dictionary query has no
   blittable P/Invoke form and the tool's output is documented and stable.
4. **libsecret** (secret-tool(1), libsecret 0.18 and later, the freedesktop.org Secret Service API 0.2):
   `secret-tool search --unlock service <name>`, the first item whose `service` attribute is the name. Its
   `secret = ` line is the password and `attribute.user = `, failing that `attribute.username = `, the user; both
   outputs are read, since versions differ in which one carries the attributes. A non-zero exit or no secret is no
   credential. `secret-tool store --label=<label> service <name> user <user>` stores one. The tool is used rather than
   `libsecret-1`'s `secret_password_lookup_sync`, a C varargs function with no P/Invoke form, or a hand-built D-Bus
   client, which would be a whole session bus and Secret Service session protocol for one read.
5. **Testing and coverage.** The three stores are tested against recorded tool output and fake `CredReadW`
   results. The two adapters that touch the OS, `NativeWindowsCredentialReader` (advapi32) and `ProcessCommandRunner`
   (starting `security` or `secret-tool`), are thin and left out of the coverage measure with a justification, as
   ADR-0083 leaves thin native adapters out.

## Left out

- Writing to a store: the option only reads; the platforms' own tools store credentials.
- Other credential types (Windows domain credentials, keychain internet passwords): a generic item is what a user
  stores for a tool by name, and one type per platform keeps the name unambiguous.
- KWallet without the Secret Service interface: KDE Plasma 5.97 and later provide the Secret Service, which libsecret
  reads.

## Consequences

A user stores `alice` and her password once and runs `curl --user-from-keychain example.com https://example.com/`.
With `CURL_EXTENSIONS=0` the option is unknown and curl exits 2, as for any unknown option.
