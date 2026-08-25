#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct VisualComparisonPixelBounds
    {
        internal VisualComparisonPixelBounds(
            int matchedPixelCount,
            int minX,
            int minY,
            int maxX,
            int maxY)
        {
            MatchedPixelCount = matchedPixelCount;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        internal bool HasMatches => MatchedPixelCount > 0;
        internal int MatchedPixelCount { get; }
        internal int MinX { get; }
        internal int MinY { get; }
        internal int MaxX { get; }
        internal int MaxY { get; }
    }

    internal static class VisualComparisonPixelBoundsCalculator
    {
        internal static bool TryCalculate(
            Color32[] pixels,
            int width,
            int height,
            Func<Color32, bool> pixelPredicate,
            out VisualComparisonPixelBounds bounds)
        {
            bounds = default;
            long requiredPixelCount = (long)width * height;
            if (pixels == null ||
                width <= 0 ||
                height <= 0 ||
                requiredPixelCount > pixels.Length ||
                pixelPredicate == null)
            {
                return false;
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            int matchedPixelCount = 0;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    matchedPixelCount++;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            bounds = new VisualComparisonPixelBounds(
                matchedPixelCount,
                minX,
                minY,
                maxX,
                maxY);
            return true;
        }
    }
}
#endif
