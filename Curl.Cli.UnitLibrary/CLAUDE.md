# Curl.Cli.UnitLibrary

Phase 1. What lives here and how it fits together is in `README.md` beside this file.

Phase 1 will hold the 274-option table, argument parsing, .curlrc and -K, --variable,
usage text and exit-code mapping.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

Add an option by adding a row to `CommandLineOptionTable` and the property it sets on
`CommandLineOptions`; do not special-case an option inside `CommandLineParser`. The parser
hands every value to the row's applier unchanged and never refuses a value itself: build a
text option with `CommandLineOption.Text` (refuses an empty value as blank), a file-name option
with `CommandLineOption.FileName` (as `Text`, plus curl's looks-like-a-flag warning) and a numeric
option with `CommandLineOption.Value` and an applier built on `CommandLineNumber` (refuses an
empty value as not a proper number), because curl's blank-value refusal depends on the type.

Every refusal and warning text is curl's, byte for byte, and is checked against a real curl run
before it is written. Refusal and warning lines carry no line terminator; the console layer chooses
the newline.

`UploadUrl` is pure string work: it never reads the file system, and it never parses,
validates or normalises the URL. That is the URL layer's job, run where `UploadUrl` is
called (ADR-0004).
