# ADR-1376: XMPP disco output holds LF line ends and entitized CR on every platform

- Status: Accepted
- Date: 2026-11-25
- Task: BL-5928
- Decided by Claude under Stewart's delegation

## Context

An `xmpp://` or `xmpps://` disco query writes the answer's payload elements to the output (ADR-1068).
`XElement.ToString(SaveOptions.DisableFormatting)` replaces each line end in text with the platform's newline, so the
same answer gave CR LF on Windows and LF elsewhere, and a CR sent as `&#xD;` could not be told from a plain line end.
XMPP is an extended protocol with no curl output to match, so the XML rules decide.

## Decision

The payload is serialised with `NewLineHandling.Entitize`, as the stanzas sent are (BL-5916), on every platform:

- a literal line end in the server's XML is read as LF (XML 1.0, 2.11) and written as LF;
- a CR in text or an attribute value (sent as a character reference) is written as `&#xD;`, and an LF in an attribute
  value as `&#xA;`;
- the output ends with one LF.

## Consequences

The output is the same bytes on every platform, and parsing it gives back the characters the answer held. Writing the
platform's newline was rejected: it makes scripts platform-dependent and loses the difference between CR and LF.
