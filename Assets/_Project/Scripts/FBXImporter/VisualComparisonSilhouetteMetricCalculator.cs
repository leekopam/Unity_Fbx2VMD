#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct VisualComparisonSilhouetteGeometry
    {
        internal VisualComparisonSilhouetteGeometry(
            VisualComparisonPixelBounds bounds,
            float bboxHeightRatio,
            float bboxWidthRatio,
            float centerX,
            float bottomGapRatio,
            float topGapRatio,
            float[] keypointProfile)
        {
            Bounds = bounds;
            BBoxHeightRatio = bboxHeightRatio;
            BBoxWidthRatio = bboxWidthRatio;
            CenterX = centerX;
            BottomGapRatio = bottomGapRatio;
            TopGapRatio = topGapRatio;
            KeypointProfile = keypointProfile ?? Array.Empty<float>();
        }

        internal VisualComparisonPixelBounds Bounds { get; }

        internal float BBoxHeightRatio { get; }

        internal float BBoxWidthRatio { get; }

        internal float CenterX { get; }

        internal float BottomGapRatio { get; }

        internal float TopGapRatio { get; }

        internal float[] KeypointProfile { get; }
    }

    internal readonly struct VisualComparisonSilhouetteBandMetrics
    {
        internal VisualComparisonSilhouetteBandMetrics(
            float upperSpanRatio,
            float lowerSpanRatio,
            float[] spanProfile,
            float[] endpointProfile)
        {
            UpperSpanRatio = upperSpanRatio;
            LowerSpanRatio = lowerSpanRatio;
            SpanProfile = spanProfile ?? Array.Empty<float>();
            EndpointProfile = endpointProfile ?? Array.Empty<float>();
        }

        internal float UpperSpanRatio { get; }

        internal float LowerSpanRatio { get; }

        internal float[] SpanProfile { get; }

        internal float[] EndpointProfile { get; }
    }

    internal static class VisualComparisonSilhouetteMetricCalculator
    {
        internal static bool TryCalculateGeometry(
            Color32[] pixels,
            int width,
            int height,
            int bandCount,
            Func<Color32, bool> pixelPredicate,
            out VisualComparisonSilhouetteGeometry geometry)
        {
            geometry = default;
            if (!VisualComparisonPixelBoundsCalculator.TryCalculate(
                    pixels,
                    width,
                    height,
                    pixelPredicate,
                    out VisualComparisonPixelBounds bounds))
            {
                return false;
            }

            if (!bounds.HasMatches)
            {
                geometry = new VisualComparisonSilhouetteGeometry(
                    bounds,
                    float.NaN,
                    float.NaN,
                    float.NaN,
                    float.NaN,
                    float.NaN,
                    Array.Empty<float>());
                return true;
            }

            geometry = new VisualComparisonSilhouetteGeometry(
                bounds,
                (bounds.MaxY - bounds.MinY + 1) / (float)height,
                (bounds.MaxX - bounds.MinX + 1) / (float)width,
                ((bounds.MinX + bounds.MaxX + 1) * 0.5f) / width,
                bounds.MinY / (float)height,
                (height - bounds.MaxY - 1) / (float)height,
                BuildKeypointProfile(
                    pixels,
                    width,
                    height,
                    bounds.MinY,
                    bounds.MaxY,
                    bandCount,
                    pixelPredicate));
            return true;
        }

        internal static bool TryCalculateBandMetrics(
            Color32[] pixels,
            int width,
            int height,
            VisualComparisonPixelBounds bounds,
            int bandCount,
            Func<Color32, bool> pixelPredicate,
            out VisualComparisonSilhouetteBandMetrics metrics)
        {
            metrics = default;
            if (!HasValidBounds(pixels, width, height, bounds, bandCount, pixelPredicate))
            {
                return false;
            }

            CalculateUpperAndLowerSpanRatios(
                pixels,
                width,
                bounds.MinY,
                bounds.MaxY,
                pixelPredicate,
                out float upperSpanRatio,
                out float lowerSpanRatio);
            metrics = new VisualComparisonSilhouetteBandMetrics(
                upperSpanRatio,
                lowerSpanRatio,
                BuildSpanProfile(
                    pixels,
                    width,
                    bounds.MinY,
                    bounds.MaxY,
                    bandCount,
                    pixelPredicate),
                BuildEndpointProfile(
                    pixels,
                    width,
                    bounds.MinY,
                    bounds.MaxY,
                    bandCount,
                    pixelPredicate));
            return true;
        }

        private static void CalculateUpperAndLowerSpanRatios(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            Func<Color32, bool> pixelPredicate,
            out float upperSpanRatio,
            out float lowerSpanRatio)
        {
            upperSpanRatio = float.NaN;
            lowerSpanRatio = float.NaN;
            int bboxHeight = maxY - minY + 1;
            int upperStartY = minY + (int)Math.Ceiling(bboxHeight * 0.5f);
            int lowerMinX = width;
            int lowerMaxX = -1;
            int upperMinX = width;
            int upperMaxX = -1;
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                bool upperBand = y >= upperStartY;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    if (upperBand)
                    {
                        upperMinX = Math.Min(upperMinX, x);
                        upperMaxX = Math.Max(upperMaxX, x);
                    }
                    else
                    {
                        lowerMinX = Math.Min(lowerMinX, x);
                        lowerMaxX = Math.Max(lowerMaxX, x);
                    }
                }
            }

            if (upperMaxX >= upperMinX)
            {
                upperSpanRatio = (upperMaxX - upperMinX + 1) / (float)width;
            }

            if (lowerMaxX >= lowerMinX)
            {
                lowerSpanRatio = (lowerMaxX - lowerMinX + 1) / (float)width;
            }
        }

        private static float[] BuildSpanProfile(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount,
            Func<Color32, bool> pixelPredicate)
        {
            CalculateBandBounds(
                pixels,
                width,
                minY,
                maxY,
                bandCount,
                pixelPredicate,
                out int[] minXByBand,
                out int[] maxXByBand,
                out _,
                out _);
            var profile = new float[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                profile[i] = maxXByBand[i] >= minXByBand[i]
                    ? (maxXByBand[i] - minXByBand[i] + 1) / (float)width
                    : 0f;
            }

            return profile;
        }

        private static float[] BuildEndpointProfile(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount,
            Func<Color32, bool> pixelPredicate)
        {
            CalculateBandBounds(
                pixels,
                width,
                minY,
                maxY,
                bandCount,
                pixelPredicate,
                out int[] minXByBand,
                out int[] maxXByBand,
                out _,
                out _);
            var endpoints = new float[bandCount * 2];
            for (int i = 0; i < bandCount; i++)
            {
                int leftIndex = i * 2;
                int rightIndex = leftIndex + 1;
                if (maxXByBand[i] >= minXByBand[i])
                {
                    endpoints[leftIndex] = minXByBand[i] / (float)width;
                    endpoints[rightIndex] = (maxXByBand[i] + 1) / (float)width;
                }
                else
                {
                    endpoints[leftIndex] = float.NaN;
                    endpoints[rightIndex] = float.NaN;
                }
            }

            return endpoints;
        }

        private static float[] BuildKeypointProfile(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            int bandCount,
            Func<Color32, bool> pixelPredicate)
        {
            var keypoints = new List<float>((2 + (bandCount * 2)) * 2);
            AppendBBoxCenterlineEndpoint(
                pixels,
                width,
                height,
                minY,
                maxY,
                useBottomEndpoint: true,
                keypoints,
                pixelPredicate);
            AppendBBoxCenterlineEndpoint(
                pixels,
                width,
                height,
                minY,
                maxY,
                useBottomEndpoint: false,
                keypoints,
                pixelPredicate);
            CalculateBandBounds(
                pixels,
                width,
                minY,
                maxY,
                bandCount,
                pixelPredicate,
                out int[] minXByBand,
                out int[] maxXByBand,
                out int[] minYByBand,
                out int[] maxYByBand);

            for (int i = 0; i < bandCount; i++)
            {
                if (maxXByBand[i] >= minXByBand[i])
                {
                    float y = ((minYByBand[i] + maxYByBand[i] + 1) * 0.5f) / height;
                    AppendKeypoint(keypoints, minXByBand[i] / (float)width, y);
                    AppendKeypoint(keypoints, (maxXByBand[i] + 1) / (float)width, y);
                }
                else
                {
                    AppendMissingKeypoint(keypoints);
                    AppendMissingKeypoint(keypoints);
                }
            }

            return keypoints.ToArray();
        }

        private static void CalculateBandBounds(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount,
            Func<Color32, bool> pixelPredicate,
            out int[] minXByBand,
            out int[] maxXByBand,
            out int[] minYByBand,
            out int[] maxYByBand)
        {
            int bboxHeight = maxY - minY + 1;
            minXByBand = new int[bandCount];
            maxXByBand = new int[bandCount];
            minYByBand = new int[bandCount];
            maxYByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
                minYByBand[i] = int.MaxValue;
                maxYByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Math.Min(
                    bandCount - 1,
                    Math.Max(0, ((y - minY) * bandCount) / bboxHeight));
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Math.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Math.Max(maxXByBand[bandIndex], x);
                    minYByBand[bandIndex] = Math.Min(minYByBand[bandIndex], y);
                    maxYByBand[bandIndex] = Math.Max(maxYByBand[bandIndex], y);
                }
            }
        }

        private static void AppendBBoxCenterlineEndpoint(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            bool useBottomEndpoint,
            List<float> keypoints,
            Func<Color32, bool> pixelPredicate)
        {
            int minX = width;
            int maxX = -1;
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                }
            }

            if (maxX >= minX)
            {
                float endpointY = (useBottomEndpoint ? minY : maxY) / (float)height;
                AppendKeypoint(keypoints, ((minX + maxX + 1) * 0.5f) / width, endpointY);
            }
            else
            {
                AppendMissingKeypoint(keypoints);
            }
        }

        private static bool HasValidBounds(
            Color32[] pixels,
            int width,
            int height,
            VisualComparisonPixelBounds bounds,
            int bandCount,
            Func<Color32, bool> pixelPredicate)
        {
            long requiredPixelCount = (long)width * height;
            return pixels != null &&
                   width > 0 &&
                   height > 0 &&
                   requiredPixelCount <= pixels.Length &&
                   bounds.HasMatches &&
                   bounds.MinY >= 0 &&
                   bounds.MaxY < height &&
                   bounds.MaxY >= bounds.MinY &&
                   bandCount > 0 &&
                   pixelPredicate != null;
        }

        private static void AppendKeypoint(List<float> keypoints, float x, float y)
        {
            keypoints.Add(x);
            keypoints.Add(y);
        }

        private static void AppendMissingKeypoint(List<float> keypoints)
        {
            keypoints.Add(float.NaN);
            keypoints.Add(float.NaN);
        }
    }
}
#endif
