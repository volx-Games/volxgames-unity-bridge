using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeJson
    {
        public static string SerializeObject(object value)
        {
            var builder = new StringBuilder(256);
            AppendValue(builder, value);
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            switch (value)
            {
                case string text:
                    AppendString(builder, text);
                    return;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return;
            }

            if (value is int || value is long || value is float || value is double || value is decimal ||
                value is uint || value is ulong || value is short || value is ushort || value is byte || value is sbyte)
            {
                builder.Append(System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is IDictionary))
            {
                builder.Append('[');
                var first = true;
                foreach (var item in enumerable)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    AppendValue(builder, item);
                    first = false;
                }

                builder.Append(']');
                return;
            }

            builder.Append('{');
            var wroteAny = false;
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (wroteAny)
                {
                    builder.Append(',');
                }

                AppendString(builder, field.Name);
                builder.Append(':');
                AppendValue(builder, field.GetValue(value));
                wroteAny = true;
            }

            builder.Append('}');
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
