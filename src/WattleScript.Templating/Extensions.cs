using System.ComponentModel;
using System;

namespace WattleScript.Templating;

internal static class Extensions
{
    public static string ToDescriptionString(this Enum val)
    {
        DescriptionAttribute[] attributes = (DescriptionAttribute[])val.GetType().GetField(val.ToString())?.GetCustomAttributes(typeof(DescriptionAttribute), false)!;
        return attributes.Length > 0 ? attributes[0].Description : "";
    }
    
    public static T? Peek<T>(this List<T?> list)
    {
        return list.Count > 0 ? list[^1] : default;
    }
    
    public static T? Pop<T>(this List<T?> list)
    {
        if (list.Count > 0)
        {
            T? itm = list[^1];
            list.RemoveAt(list.Count - 1);
            return itm;
        }

        return default;
    }

    public static void Push<T>(this List<T?> list, T? itm)
    {
        list.Add(itm);
    }
    
    public static string ReplaceFirst(this string text, string search, string replace)
    {
        int pos = text.IndexOf(search, StringComparison.Ordinal);
        return pos < 0 ? text : string.Concat(text[..pos], replace, text.AsSpan(pos + search.Length));
    }
} 