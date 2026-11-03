namespace Curl.Conformance.Extended.Amqp1;

/// <summary>The descriptor codes of the composite types the reference container reads and writes (Part 2 section 2.7, 2.8.14, Part 3 section 3.5, Part 5 section 5.3.3).</summary>
public static class Amqp1ReferenceDescriptors
{
    /// <summary>open, 0x10.</summary>
    public const ulong Open = 0x10;

    /// <summary>begin, 0x11.</summary>
    public const ulong Begin = 0x11;

    /// <summary>attach, 0x12.</summary>
    public const ulong Attach = 0x12;

    /// <summary>flow, 0x13.</summary>
    public const ulong Flow = 0x13;

    /// <summary>transfer, 0x14.</summary>
    public const ulong Transfer = 0x14;

    /// <summary>disposition, 0x15.</summary>
    public const ulong Disposition = 0x15;

    /// <summary>detach, 0x16.</summary>
    public const ulong Detach = 0x16;

    /// <summary>end, 0x17.</summary>
    public const ulong End = 0x17;

    /// <summary>close, 0x18.</summary>
    public const ulong Close = 0x18;

    /// <summary>error, 0x1d.</summary>
    public const ulong Error = 0x1d;

    /// <summary>source, 0x28.</summary>
    public const ulong Source = 0x28;

    /// <summary>target, 0x29.</summary>
    public const ulong Target = 0x29;

    /// <summary>sasl-mechanisms, 0x40.</summary>
    public const ulong SaslMechanisms = 0x40;

    /// <summary>sasl-init, 0x41.</summary>
    public const ulong SaslInit = 0x41;

    /// <summary>sasl-outcome, 0x44.</summary>
    public const ulong SaslOutcome = 0x44;
}
