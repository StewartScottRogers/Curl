# Curl.Extension.RecordReplay.UnitLibrary

The record and replay extension (ADR-0999, ADR-1129). Namespace `Curl.Extension.RecordReplay`.
It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `RecordReplayExtension` gives the `--record <dir>` and `--replay <dir>` options, the description and the hook
  activator, which `Curl.Extension.Composition` registers in its option, description and transfer hook catalogues.
- `RecordReplayTransferHook` answers an HTTP transfer from the replay directory without the server (ADR-1129
  section 5), fails one with no recording with exit 7 unless `--record` is given too, and records what goes out
  (section 4). A transfer that is not HTTP runs as it would without the hook.
- `ExchangeKey` gives a request's method and its key (section 2); `ExchangeDirectory` writes and reads one
  `RecordedExchange` per `<key>.exchange` file (section 1), encoded by `RecordedExchangeEncoding` (section 3); a
  file that is not a whole exchange reads as no recording.

Tested against ADR-1129's record format, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.RecordReplay.UnitTests`.
