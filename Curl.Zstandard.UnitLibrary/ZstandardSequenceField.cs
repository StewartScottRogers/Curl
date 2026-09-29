namespace Curl.Zstandard;

/// <summary>
/// One of the three codes a sequence is made of - literals length, offset or match length
/// (RFC 8878 section 3.1.1.3.2.1) - with the limits on its FSE table and the table its
/// <c>Predefined_Mode</c> uses.
/// </summary>
/// <param name="MaxSymbol">The largest code the field has.</param>
/// <param name="MaxAccuracyLog">The largest <c>Accuracy_Log</c> an <c>FSE_Compressed_Mode</c> table of the field may have.</param>
/// <param name="PredefinedTable">The table built from the field's default distribution (RFC 8878 section 3.1.1.3.2.2).</param>
internal sealed record ZstandardSequenceField(int MaxSymbol, int MaxAccuracyLog, ZstandardFseTable PredefinedTable)
{
    /// <summary>Literals length codes 0 to 35; default distribution of RFC 8878 section 3.1.1.3.2.2.1.</summary>
    public static ZstandardSequenceField LiteralsLength { get; } = new(35, 9, ZstandardFseTable.Build(
    [
        4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1,
        -1, -1, -1, -1,
    ], 6));

    /// <summary>Offset codes 0 to 31; default distribution of RFC 8878 section 3.1.1.3.2.2.3, which stops at code 28.</summary>
    public static ZstandardSequenceField Offset { get; } = new(31, 8, ZstandardFseTable.Build(
    [
        1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1,
    ], 5));

    /// <summary>Match length codes 0 to 52; default distribution of RFC 8878 section 3.1.1.3.2.2.2.</summary>
    public static ZstandardSequenceField MatchLength { get; } = new(52, 9, ZstandardFseTable.Build(
    [
        1, 4, 3, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1,
        -1, -1, -1, -1, -1,
    ], 6));
}
