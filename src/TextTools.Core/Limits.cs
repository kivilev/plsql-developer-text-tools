namespace TextTools.Core;

/// <summary>Safety limits shared by all transformations.</summary>
public static class Limits
{
    /// <summary>
    /// Every regex has a match timeout: a pathological selection must not freeze the IDE's UI thread.
    /// A timeout surfaces as RegexMatchTimeoutException and is reported in the status bar.
    /// </summary>
    public const int RegexTimeoutMs = 2000;
}
