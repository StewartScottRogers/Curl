namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>One <c>curl_mime_*</c> call on a part of a MIME case.</summary>
/// <param name="Kind">The call.</param>
/// <param name="Text">The call's text argument (hexadecimal bytes for <see cref="LibcurlMimeStepKind.Data"/>, a file name for <see cref="LibcurlMimeStepKind.File"/>); <see langword="null"/> passes a null pointer.</param>
/// <param name="Headers">The header lines of a <see cref="LibcurlMimeStepKind.Headers"/> call.</param>
/// <param name="SubParts">The parts of a <see cref="LibcurlMimeStepKind.SubParts"/> call's MIME structure.</param>
public sealed record LibcurlMimeStep(LibcurlMimeStepKind Kind, string? Text, IReadOnlyList<string> Headers, IReadOnlyList<LibcurlMimePart> SubParts);
