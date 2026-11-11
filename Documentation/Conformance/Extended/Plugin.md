# Plugin: the curl-proto-&lt;scheme&gt; helper program contract

Curl's helper-program extension (`Curl.Extended.Protocol.Plugin.UnitLibrary`) measured against its own contract,
ADR-1142's "Decision" (git's gitremote-helpers "INVOCATION", "Capabilities" and "Commands", adapted), BL-4489. The
cases run curl in process with helper files in a temporary directory on a `PATH` the stand-in process starter gives
the search, and a stand-in helper (`Curl.Conformance.Extended.UnitLibrary/Plugin/PluginReferenceHelper`) that records
what Curl writes and answers as the contract says; no real process starts. A helper's messages name its file, so on
Windows they read `curl-proto-demo.cmd`. Cases listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` fail
today and name the task that fixes them.

| ID | Level | Requirement | Covered by |
|----|:-----:|-------------|------------|
| Plugin:ADR1142-Discovery-1 | MUST | Every `PATH` directory's `curl-proto-*` files are listed, and a file serves the scheme after the prefix. | PluginDiscoveryCases.FirstPathDirectoryWins_EmptyAndMissingEntriesSkipped |
| Plugin:ADR1142-Discovery-2 | MUST | The name after the prefix serves only when it is an RFC 3986 scheme (`ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )`), lowercased. | PluginDiscoveryCases.SchemeWithUppercaseLettersDigitsAndPunctuation_IsServedLowercased |
| Plugin:ADR1142-Discovery-3 | MUST | On Windows `PATH` is `;`-separated, the prefix matches without case and the name ends in a `PATHEXT` extension (default `.COM;.EXE;.BAT;.CMD`), dropped; elsewhere `PATH` is `:`-separated and the whole rest is the scheme. A `.bat` or `.cmd` helper runs as `cmd.exe /e:ON /v:OFF /d /c ""<program>" "<url>""` with every `%` neutralised, so no character in the URL starts another command or expands a variable; a URL holding a double quote, carriage return, line feed or NUL is refused with exit 7 before the helper starts (ADR-1238). | PluginDiscoveryCases.Windows_PrefixMatchesWithoutCaseAndPathextExtensionIsDropped |
| Plugin:ADR1142-Discovery-4 | MUST | The first directory holding a scheme's program wins. | PluginDiscoveryCases.FirstPathDirectoryWins_EmptyAndMissingEntriesSkipped |
| Plugin:ADR1142-Discovery-5 | MUST | Empty `PATH` entries and missing or unreadable directories are skipped. | PluginDiscoveryCases.FirstPathDirectoryWins_EmptyAndMissingEntriesSkipped |
| Plugin:ADR1142-Reserved-1 | MUST NOT | A helper is never taken for one of curl's own schemes: a `curl-proto-http` is ignored. | PluginDiscoveryCases.HelperForCurlsOrAnotherExtensionsScheme_IsIgnored |
| Plugin:ADR1142-Reserved-2 | MUST NOT | A helper is never taken for a scheme another extension serves. | PluginDiscoveryCases.HelperForCurlsOrAnotherExtensionsScheme_IsIgnored |
| Plugin:ADR1142-Reserved-3 | MUST | With no helper for a scheme it is refused as an unknown scheme: exit 1, `curl: (1) Protocol "demo" not supported`. | PluginDiscoveryCases.NoHelperForTheScheme_Exit1ProtocolNotSupported |
| Plugin:ADR1142-Reserved-4 | MUST | Under `CURL_EXTENSIONS=0` no helper runs and the scheme is refused with exit 1. | PluginDiscoveryCases.CurlExtensionsZero_Exit1ProtocolNotSupported |
| Plugin:ADR1142-Invocation-1 | MUST | The program runs with the URL as typed as its one argument. | PluginDiscoveryCases.FirstPathDirectoryWins_EmptyAndMissingEntriesSkipped |
| Plugin:ADR1142-Invocation-2 | MUST | The helper's standard error is Curl's. | n/a: the helper inherits the stream from the operating system's process start (`SystemHelperProcessStarter`, excluded from coverage by ADR-0083); Curl never reads or writes it, so no in-process case can observe it |
| Plugin:ADR1142-Capabilities-1 | MUST | Curl writes `capabilities` and a newline, and reads one capability per line up to an empty line. | PluginExchangeCases.Get_WritesCapabilitiesThenGetAndWritesTheBody |
| Plugin:ADR1142-Capabilities-2 | MUST | Curl knows `get` and `put`; an unknown capability without `*` is ignored. | PluginExchangeCases.UnknownOptionalCapability_IsIgnored |
| Plugin:ADR1142-Capabilities-3 | MUST | A mandatory (`*`) capability Curl does not know ends the transfer with exit 1, `<helper> requires the unknown capability <name>`; a known one is accepted. | PluginExchangeCases.UnknownMandatoryCapability_Exit1BeforeAnyCommand |
| Plugin:ADR1142-Commands-1 | MUST | A transfer with neither `-T` nor `-d` writes `get <url>`. | PluginExchangeCases.Get_WritesCapabilitiesThenGetAndWritesTheBody |
| Plugin:ADR1142-Commands-2 | MUST | A transfer with an upload writes `put <url>` and then the `-T` file or the `-d` data. | PluginExchangeCases.PutWithData_WritesPutThenTheData |
| Plugin:ADR1142-Commands-3 | MUST | Curl then closes the helper's input, writing nothing more. | PluginExchangeCases.PutWithData_WritesPutThenTheData |
| Plugin:ADR1142-Commands-4 | MUST | A helper lacking the capability the transfer needs fails it with exit 1, `<helper> does not offer the <command> capability`. | PluginExchangeCases.MissingCapabilityTheTransferNeeds_Exit1 |
| Plugin:ADR1142-Reply-1 | MUST | After an `ok` line everything to the end of the helper's output is the body Curl writes. | PluginExchangeCases.Get_WritesCapabilitiesThenGetAndWritesTheBody |
| Plugin:ADR1142-Reply-2 | MUST | `error <message>` is exit 56, `<helper>: <message>`. | PluginReplyCases.ErrorReply_Exit56WithTheMessage |
| Plugin:ADR1142-Reply-3 | MUST | Any other reply, or the output ending first, is exit 8. | PluginReplyCases.ReplyNeitherOkNorError_Exit8 |
| Plugin:ADR1142-Reply-4 | MUST | Lines end with LF; a CR before it is dropped. | PluginReplyCases.CrlfLines_AreReadAsLfLines |
| Plugin:ADR1142-Reply-5 | MUST | A line before the body is at most 8192 bytes; a longer one is exit 8. | PluginReplyCases.LineOf8193Bytes_Exit8 |
| Plugin:ADR1142-Exit-1 | MUST | A helper exiting non-zero after its body fails the transfer with exit 56, `<helper> exited with status <n>`. | PluginReplyCases.HelperExitsNonZeroAfterItsBody_Exit56 |
| Plugin:ADR1142-Exit-2 | MUST | A program that cannot be started is exit 7, `Failed to run <helper>: <reason>`. | PluginReplyCases.HelperThatCannotStart_Exit7 |
| Plugin:ADR1142-Exit-3 | MUST | A pipe breaking mid-conversation is exit 56. | PluginReplyCases.PipeBreaksMidConversation_Exit56 |
