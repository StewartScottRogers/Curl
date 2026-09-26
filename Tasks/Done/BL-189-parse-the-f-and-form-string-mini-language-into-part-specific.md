---
id: BL-189
title: Parse the -F and --form-string mini-language into part specifications
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-189 — Parse the -F and --form-string mini-language into part specifications

## Goal

`-F`/`--form` and `--form-string` parse into ordered part specifications (name, value or file, type, filename, headers).

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-F a=b -F f=@srv.py` body: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `name=value`, `name=@file`, `name=<file`, `;type=`, `;filename=`, `;headers=` and quoting parse as measured on curl 8.21.0.
- [x] A malformed spec gives the measured refusal and exit.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered (2026-09-26): `-F`/`--form` and `--form-string` rows in `CommandLineOptionTable`; `MultipartFormField` ports curl 8.21.0's `formparse` and `FormPartParameterReader` its `get_param_part`/`get_param_word` (`src/tool_formparse.c` at tag `curl-8_21_0`, read before porting); the result is `CommandLineOptions.FormParts`, a tree of `FormPartSpecification` (`Name`, `Kind` = `Text`/`FileUpload`/`FileContent`/`Multipart`, `Content`, `ContentType`, `FileName`, `Encoder`, `Headers`, `Parts`). Nested `name=(` ... `=)` multiparts and `@file1,file2` groups are included because they are part of the same mini-language. 74 tests in `Curl.Cli.UnitTests/CommandLineFormOptionTests.cs`.
- Measured: curl 8.21.0 (`/mingw64/bin/curl`, Schannel) with `Record-CurlExchange.ps1 -Port 18189 -CurlArgs --no-progress-meter,-F,<value>,http://127.0.0.1:18189/` in a scratch folder holding `f.txt` (`hi`), `g.txt` (`y`) and header files; refusals against `http://127.0.0.1:1/`. Every case pinned in the tests was run; a sample of the recorded bytes: `-F a=@f.txt;type=image/png;filename=q` sent `Content-Disposition: form-data; name="a"; filename="q"`, CRLF, `Content-Type: image/png`, CRLF, CRLF, `hi`; `-F 'a=b;headers=X-A: 1'` sent `Content-Disposition: form-data; name="a"`, CRLF, `X-A: 1`, CRLF, CRLF, `b`; `-F a=@f.txt,g.txt` sent a `multipart/mixed` part named `a` holding two `attachment` parts; a header file of the lines `# c` (CRLF-ended), `X-A: 1  `, `  folded`, an empty line and `X-B:2` gave `X-A: 1  folded` and `X-B:2`.
- Refusals and warnings measured (all exit 2): `-F abc`, `-F ''`, `--form=`, `--form-string abc` -> `Warning: Illegally formatted input field` + `curl: option <as typed>: is badly used here` + try-help; `-F =)` with no multipart open -> `Warning: no multipart to terminate` + the same two lines; `-s` before drops the warning. `-F` against `-I`/`--no-head` either way -> the two-line `Warning: You can only select one HTTP request method! You asked for both <later> and <earlier>.` + `is badly used here` for the later option. `-F` with any `-d`/`--data-*`/`--json` body, either order -> only the two warning lines (`POST (-d, --data) and multipart formpost (-F, --form)`, or `GET (-G, --get) ...` with `-G`), no option line, no try-help, after the no-URL check; `-s` anywhere prints nothing. Warnings that do not refuse: `Trailing data after quoted form parameter`, `skip unknown form field: <piece>`, `garbage at end of field specification: <rest>`, `Field filename not allowed here: <v>`, `Field encoder not allowed here: <v>`, `Cannot read from <file>: No such file or directory`. `--no-form`, `--no-form=x`, `--no-form-string`, `--no-form-string=a=b` -> `the given option cannot be reversed with a --no- prefix`.
- Decisions (made by Claude under Stewart's delegation; the ADR is BL-258, because `Documentation/Planning/Decisions` is held by BL-053 in another lane and is outside this task's `touches`): (1) `@file`, `<file`, `@-` and `<-` are named, not read, while parsing; the transfer layer reads them. (2) An unreadable `;headers=@file` always warns `No such file or directory`, since `IDataFileReader` does not say why a read failed. (3) Header files are decoded as UTF-8 and split at LF, not through C text mode. (4) `-F` with a `-d` body is refused after the whole command line is read (`CommandLineRefusal.FormAndDataBoth`, no lines of its own), as curl reports it.
- Also changed: `SelectedHttpMethod` gained `MultipartFormPost` and `Post`; the fixed `HeadRequestedAfterGet`/`GetRequestedAfterHead` warnings became the general `CommandLineWarning.OnlyOneRequestMethod(requested, selected)`, which `-I`/`--no-head` now use too (same bytes, existing tests unchanged). BL-255 in Backlog still names the old two properties; it should build on `OnlyOneRequestMethod` instead.
- Not done here: `-T`/`--upload-file` is not in the option table yet, so the measured `-F` + `-T` conflict (`PUT (-T, --upload-file) and multipart formpost (-F, --form)`, exit 2) is left to whichever task adds `-T`.
- Gates: `dotnet build -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` green (Curl.Cli.UnitTests 1045 fast, the 2 Integration tests pre-existing); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -F/--form and --form-string parse into ordered FormPartSpecification trees (value, @file, <file, type, filename, headers, encoder, quoting, nested multiparts) with curl 8.21.0's measured warnings and refusals
