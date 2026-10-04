using System.Globalization;

namespace Curl.Core.Globbing;

/// <summary>
/// One piece of a parsed URL glob: literal text, a <c>{a,b}</c> set, or a character or
/// numeric range. Each has <see cref="Count" /> values, read by index so a range of
/// billions is never held in memory.
/// </summary>
internal abstract class UrlGlobPiece
{
    /// <summary>Gets how many values the piece has; at least one.</summary>
    public abstract long Count { get; }

    /// <summary>Gets whether the piece is a set or range, which <c>#N</c> counts.</summary>
    public abstract bool IsGlob { get; }

    /// <summary>
    /// Gets or sets the glob's <c>&lt;name&gt;</c>, which <c>#&lt;name&gt;</c> stands for;
    /// <see langword="null" /> for an unnamed glob or literal text.
    /// </summary>
    public string? Name { get; set; }

    public static UrlGlobPiece Fixed(string text) => new SetPiece([text], isGlob: false);

    public static UrlGlobPiece Set(IReadOnlyList<string> elements) => new SetPiece(elements, isGlob: true);

    public static UrlGlobPiece CharacterRange(char first, int step, long count) =>
        new CharacterRangePiece(first, step, count);

    public static UrlGlobPiece NumberRange(long first, long step, long count, int padLength) =>
        new NumberRangePiece(first, step, count, padLength);

    /// <summary>Gets value <paramref name="index" />, from zero to <see cref="Count" /> less one.</summary>
    public abstract string ValueAt(long index);

    private sealed class SetPiece(IReadOnlyList<string> elements, bool isGlob) : UrlGlobPiece
    {
        public override long Count => elements.Count;

        public override bool IsGlob => isGlob;

        public override string ValueAt(long index) => elements[(int)index];
    }

    private sealed class CharacterRangePiece(char first, int step, long count) : UrlGlobPiece
    {
        public override long Count => count;

        public override bool IsGlob => true;

        public override string ValueAt(long index) => ((char)(first + (index * step))).ToString();
    }

    private sealed class NumberRangePiece(long first, long step, long count, int padLength) : UrlGlobPiece
    {
        public override long Count => count;

        public override bool IsGlob => true;

        public override string ValueAt(long index) =>
            (first + (index * step)).ToString(CultureInfo.InvariantCulture).PadLeft(padLength, '0');
    }
}
