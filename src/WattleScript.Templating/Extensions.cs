using System.ComponentModel;

namespace WattleScript.Templating;

internal static class Extensions
{
    public static string ToDescriptionString(this Enum val)
    {
        DescriptionAttribute[] attributes = (DescriptionAttribute[])val.GetType().GetField(val.ToString())?.GetCustomAttributes(typeof(DescriptionAttribute), false)!;
        return attributes.Length > 0 ? attributes[0].Description : "";
    }
} 