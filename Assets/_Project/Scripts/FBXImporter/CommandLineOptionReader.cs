using System;
using System.Globalization;

namespace Fbx2Vmd.FBXImporter
{
    internal static class CommandLineOptionReader
    {
        internal static string ReadValue(string[] arguments, string name, string fallbackValue)
        {
            if (arguments == null)
            {
                return fallbackValue;
            }

            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return fallbackValue;
        }

        internal static float ReadFloat(string[] arguments, string name, float fallbackValue)
        {
            string value = ReadValue(arguments, name, string.Empty);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallbackValue;
        }

        internal static int ReadInt(string[] arguments, string name, int fallbackValue)
        {
            string value = ReadValue(arguments, name, string.Empty);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallbackValue;
        }

        internal static bool ReadBool(string[] arguments, string name, bool fallbackValue)
        {
            string value = ReadValue(arguments, name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallbackValue;
            }

            if (bool.TryParse(value, out bool parsedBool))
            {
                return parsedBool;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : fallbackValue;
        }
    }
}
