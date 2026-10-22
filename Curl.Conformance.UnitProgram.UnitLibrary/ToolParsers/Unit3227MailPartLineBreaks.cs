using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core.Multipart;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit3227.c</c>: its five parts through
/// <c>MultipartPartHeaders.RefusesLineBreakInName</c>, the refusal of <c>Curl_mime_prepare_headers</c>, and
/// <c>MultipartPartHeaders.Format</c>, the headers it prepares. A CR or LF in a mail part's name or file name
/// is refused (<c>CURLE_BAD_FUNCTION_ARGUMENT</c>); a plain mail part and a form part with CR LF in its file
/// name are accepted, and their headers split no line (<c>headers_have_ctrl</c>).
/// </summary>
public sealed class Unit3227MailPartLineBreaks : IUnitProgram
{
    private const string InjectedFileName = "a\r\nX-Injected: 1";

    /// <inheritdoc/>
    public string ProgramName => "unit3227";

    /// <summary><c>headers_have_ctrl</c>: whether a header line of <paramref name="block"/> holds a raw CR or LF.</summary>
    /// <param name="block">Header lines, each ending CR LF.</param>
    /// <returns><see langword="true"/> when a line is split.</returns>
    public static bool HeadersHaveControl(string block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.Split("\r\n").Any(line => line.AsSpan().IndexOfAny('\r', '\n') >= 0);
    }

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        checks.FailUnless(RefusedForMail(Part(null, InjectedFileName)), "CRLF filename accepted for mail");
        checks.FailUnless(RefusedForMail(Part("a\nb", null)), "LF name accepted for mail");
        checks.FailUnless(RefusedForMail(Part(null, "a\rb")), "CR filename accepted for mail");

        MultipartFormPart plain = Part("field", "readme.txt");
        checks.FailUnless(!RefusedForMail(plain), "plain mail part rejected");
        string mailBlock = MultipartPartHeaders.Format(plain, null, null, null, MultipartNameEscaping.Percent, forMail: true, out _);
        checks.FailUnless(!HeadersHaveControl(mailBlock), "clean mail header split");

        MultipartFormPart form = Part(null, InjectedFileName);
        checks.FailUnless(!MultipartPartHeaders.RefusesLineBreakInName(form, MultipartNameEscaping.Percent, forMail: false), "CRLF filename rejected for form");
        string formBlock = MultipartPartHeaders.Format(form, "form-data", null, null, MultipartNameEscaping.Percent, forMail: false, out _);
        checks.FailUnless(!HeadersHaveControl(formBlock), "form header split despite encoding");
        return Task.FromResult(checks.Result);
    }

    private static bool RefusedForMail(MultipartFormPart part) =>
        MultipartPartHeaders.RefusesLineBreakInName(part, MultipartNameEscaping.Percent, forMail: true);

    private static MultipartFormPart Part(string? name, string? fileName) =>
        new(name, MultipartFormPartKind.Text, "x", null, fileName, [], []);
}
