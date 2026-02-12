using System.ComponentModel;

namespace AiCoreUtils.Common.Tools;
public static class DateTimeTools
{
    [Description("Get the current time where the user is. Don't cache the output of this tool, come back each time to get the latest time")]
    internal static string GetCurrentTime()
    => DateTime.Now.ToLongTimeString();

    [Description("Get the current date where the user is. Don't cache the output of this tool, come back each time to get the latest date")]
    internal static string GetCurrentDate()
    => DateTime.Now.ToLongDateString();
}