using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonCsvMetricReader
    {
        internal static string[] SplitLine(string line)
        {
            return (line ?? string.Empty).Split(',');
        }

        internal static Dictionary<string, int> BuildIndexMap(string[] headers)
        {
            Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                if (!indices.ContainsKey(headers[index]))
                {
                    indices.Add(headers[index], index);
                }
            }

            return indices;
        }

        internal static int FindHeaderIndex(string[] headers, string headerName)
        {
            if (headers == null)
            {
                return -1;
            }

            for (int index = 0; index < headers.Length; index++)
            {
                if (string.Equals(headers[index], headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        internal static string ReadString(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            if (row == null ||
                indices == null ||
                string.IsNullOrEmpty(column) ||
                !indices.TryGetValue(column, out int index) ||
                index < 0 ||
                index >= row.Length)
            {
                return string.Empty;
            }

            return row[index] ?? string.Empty;
        }

        internal static int ReadInt(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return int.TryParse(
                ReadString(row, indices, column),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : 0;
        }

        internal static float ReadFloat(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return float.TryParse(
                ReadString(row, indices, column),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : float.NaN;
        }
    }
}
