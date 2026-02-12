using System.ComponentModel;

namespace AiCoreUtils.Common.Tools;
public static class MathTools
{
    [Description("Adds a sequence of decimal numbers together.")]
    internal static decimal Add(IEnumerable<decimal> numbers)
    => numbers.Sum();
}