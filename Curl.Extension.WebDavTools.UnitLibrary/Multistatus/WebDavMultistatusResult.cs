namespace Curl.Extension.WebDavTools.Multistatus;

/// <summary>The outcome of reading a <c>207 Multistatus</c> body: its entries, or why it was refused.</summary>
public sealed class WebDavMultistatusResult
{
    private WebDavMultistatusResult(IReadOnlyList<WebDavResourceEntry>? entries, WebDavMultistatusFailureKind? failureKind, string? failure)
    {
        Entries = entries;
        FailureKind = failureKind;
        Failure = failure;
    }

    /// <summary>Gets one entry per <c>response</c>, in body order, or <see langword="null"/> when the body was refused.</summary>
    public IReadOnlyList<WebDavResourceEntry>? Entries { get; }

    /// <summary>Gets why the body was refused, or <see langword="null"/> when it was read.</summary>
    public WebDavMultistatusFailureKind? FailureKind { get; }

    /// <summary>Gets a description of why the body was refused, or <see langword="null"/> when it was read.</summary>
    public string? Failure { get; }

    /// <summary>Gets a value indicating whether the body was read.</summary>
    public bool Succeeded => Entries is not null;

    /// <summary>Returns the result of a body that was read.</summary>
    /// <param name="entries">One entry per <c>response</c>.</param>
    /// <returns>A succeeded result.</returns>
    public static WebDavMultistatusResult Success(IReadOnlyList<WebDavResourceEntry> entries) => new(entries, null, null);

    /// <summary>Returns the result of a body that was refused.</summary>
    /// <param name="kind">Why it was refused.</param>
    /// <param name="failure">A description of why it was refused.</param>
    /// <returns>A failed result.</returns>
    public static WebDavMultistatusResult Refused(WebDavMultistatusFailureKind kind, string failure) => new(null, kind, failure);
}
