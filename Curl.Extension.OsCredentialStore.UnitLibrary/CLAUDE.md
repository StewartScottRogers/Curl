# Curl.Extension.OsCredentialStore.UnitLibrary

The OS credential store extension (ADR-0999, ADR-1139). Namespace `Curl.Extension.OsCredentialStore`.
It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `OsCredentialStoreExtension` gives the `--user-from-keychain <name>` option, the description and the hook
  activator, which `Curl.Extension.Composition` registers in its option, description and transfer hook catalogues.
- `OsCredentialStoreTransferHook` reads the credential from the store `CredentialStores` picks for the running OS
  (ADR-1139 section 1) and runs the transfer with it through `CredentialedTransferContext`; none fails with exit 67.
- `WindowsCredentialManagerStore` (section 2) reads through `IWindowsCredentialReader`, `MacKeychainStore`
  (section 3, with `SecurityToolValue`) and `LibsecretStore` (section 4) through `ICommandRunner`.
- `NativeWindowsCredentialReader` and `ProcessCommandRunner` are the only code that touches the OS; they are thin
  and excluded from coverage per ADR-0083, so keep any logic out of them.

Tested against the three platforms' credential store interfaces, not against curl. Held to the four quality gates in
the root `CLAUDE.md`. Its tests are in `Curl.Extension.OsCredentialStore.UnitTests`.
