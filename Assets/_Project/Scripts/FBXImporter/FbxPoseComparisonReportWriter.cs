#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Runner = Fbx2Vmd.FBXImporter.FbxRuntimePoseClipCompareRunner;

namespace Fbx2Vmd.FBXImporter
{
    internal static class FbxPoseComparisonReportWriter
    {
                internal static string BuildImportVariantMeta(
                    string primaryMeta,
                    string fallbackMeta,
                    string variantName,
                    string guid = "00000000000000000000000000000000")
                {
                    string meta;
                    switch (variantName)
                    {
                        case Runner.VariantFallbackFullMeta:
                            meta = fallbackMeta;
                            break;
                        case Runner.VariantFallbackAnimationScalars:
                            meta = ReplaceScalar(
                                primaryMeta,
                                "animationCompression",
                                ExtractRequiredScalar(fallbackMeta, "animationCompression"));
                            meta = ReplaceScalar(
                                meta,
                                "animationWrapMode",
                                ExtractRequiredScalar(fallbackMeta, "animationWrapMode"));
                            break;
                        case Runner.VariantFallbackAvatarDefinition:
                            meta = ReplaceYamlBlock(primaryMeta, fallbackMeta, "humanDescription");
                            meta = ReplaceYamlBlock(meta, fallbackMeta, "skeleton");
                            break;
                        case Runner.VariantFallbackSkeletonOnly:
                            meta = ReplaceYamlBlock(primaryMeta, fallbackMeta, "skeleton");
                            break;
                        default:
                            throw new ArgumentException($"Unknown import variant: {variantName}", nameof(variantName));
                    }

                    return ReplaceGuid(meta, guid);
                }

                internal static string ResolveOutputPath(string projectRoot, string outputPath, string evidenceDirectory)
                {
                    if (!string.IsNullOrWhiteSpace(outputPath))
                    {
                        return Path.GetFullPath(Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(projectRoot, outputPath));
                    }

                    string directory = Path.Combine(
                        projectRoot,
                        evidenceDirectory,
                        $"fbx_runtime_pose_clip_compare_satisfaction2_{DateTime.Now:yyyyMMdd-HHmmss}");
                    return Path.Combine(directory, "report.json");
                }

                internal static void WriteRowsCsv(string path, IReadOnlyList<Runner.PoseComparisonRow> rows)
                {
                    var builder = new StringBuilder(rows.Count * 160);
                    builder.AppendLine("frame,timeSeconds,bone,rotationDifferenceDegrees,primaryLocalEuler,fallbackLocalEuler");
                    foreach (Runner.PoseComparisonRow row in rows)
                    {
                        builder.AppendLine(string.Join(",", new[]
                        {
                            row.Frame.ToString(CultureInfo.InvariantCulture),
                            F(row.TimeSeconds),
                            EscapeCsv(row.Bone),
                            F(row.RotationDifferenceDegrees),
                            EscapeCsv(V(row.PrimaryLocalEuler)),
                            EscapeCsv(V(row.FallbackLocalEuler)),
                        }));
                    }

                    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                }

                internal static void WriteReportJson(
                    string reportPath,
                    string csvPath,
                    AnimationClip primaryClip,
                    AnimationClip fallbackClip,
                    string primaryFbxPath,
                    string fallbackFbxPath,
                    string targetPrefabPath,
                    string controllerPath,
                    float frameRate,
                    IReadOnlyList<int> sampleFrames,
                    float highRotationDifferenceThresholdDegrees,
                    Runner.ClipComparisonSummary summary,
                    IReadOnlyList<Runner.PoseComparisonRow> rows,
                    IReadOnlyList<Runner.ImportVariantComparison> importVariants,
                    Runner.RuntimeImporterComparison runtimeImporterComparison,
                    string reportDirectory,
                    string projectRoot)
                {
                    bool sourceFbxFilesIdentical = Sha256File(Path.Combine(projectRoot, primaryFbxPath.Replace('/', Path.DirectorySeparatorChar)))
                        == Sha256File(Path.Combine(projectRoot, fallbackFbxPath.Replace('/', Path.DirectorySeparatorChar)));
                    Runner.ImportVariantCorrelationSummary variantCorrelation = Runner.BuildImportVariantCorrelationSummaryForTest(
                        importVariants,
                        closeThresholdDegrees: 1f,
                        farThresholdDegrees: highRotationDifferenceThresholdDegrees);
                    var builder = new StringBuilder(8192);
                    builder.AppendLine("{");
                    WriteJsonProperty(builder, 1, "schema", "fbx-runtime-pose-clip-compare-v1", comma: true);
                    WriteJsonProperty(builder, 1, "generated_at", DateTime.Now.ToString("O", CultureInfo.InvariantCulture), comma: true);
                    WriteJsonProperty(builder, 1, "primary_fbx_path", primaryFbxPath, comma: true);
                    WriteJsonProperty(builder, 1, "fallback_fbx_path", fallbackFbxPath, comma: true);
                    WriteJsonProperty(builder, 1, "source_fbx_files_identical_sha256", sourceFbxFilesIdentical, comma: true);
                    WriteJsonProperty(builder, 1, "target_prefab_path", targetPrefabPath, comma: true);
                    WriteJsonProperty(builder, 1, "controller_path", controllerPath, comma: true);
                    WriteJsonProperty(builder, 1, "primary_clip_name", primaryClip.name, comma: true);
                    WriteJsonProperty(builder, 1, "primary_clip_length_seconds", primaryClip.length, comma: true);
                    WriteJsonProperty(builder, 1, "primary_human_motion", primaryClip.humanMotion, comma: true);
                    WriteJsonProperty(builder, 1, "fallback_clip_name", fallbackClip.name, comma: true);
                    WriteJsonProperty(builder, 1, "fallback_clip_length_seconds", fallbackClip.length, comma: true);
                    WriteJsonProperty(builder, 1, "fallback_human_motion", fallbackClip.humanMotion, comma: true);
                    WriteJsonProperty(builder, 1, "frame_rate", frameRate, comma: true);
                    WriteJsonArray(builder, 1, "sample_frames", sampleFrames.Select(frame => frame.ToString(CultureInfo.InvariantCulture)), comma: true);
                    WriteJsonProperty(builder, 1, "rows_csv_path", MakeProjectRelativePath(projectRoot, csvPath), comma: true);
                    builder.AppendLine("  \"summary\": {");
                    WriteJsonProperty(builder, 2, "row_count", summary.RowCount, comma: true);
                    WriteJsonProperty(builder, 2, "sample_frame_count", summary.SampleFrameCount, comma: true);
                    WriteJsonProperty(builder, 2, "bone_count", summary.BoneCount, comma: true);
                    WriteJsonProperty(builder, 2, "high_rotation_difference_threshold_degrees", summary.HighRotationDifferenceThresholdDegrees, comma: true);
                    WriteJsonProperty(builder, 2, "high_rotation_difference_count", summary.HighRotationDifferenceCount, comma: true);
                    WriteJsonProperty(builder, 2, "max_rotation_difference_degrees", summary.MaxRotationDifferenceDegrees, comma: true);
                    WriteJsonProperty(builder, 2, "max_rotation_bone", summary.MaxRotationBone, comma: true);
                    WriteJsonProperty(builder, 2, "max_rotation_frame", summary.MaxRotationFrame, comma: false);
                    builder.AppendLine("  },");
                    builder.AppendLine("  \"focus_bones\": [");
                    int focusIndex = 0;
                    List<Runner.FocusBoneSummary> focusSummaries = summary.FocusBones.Values
                        .OrderBy(focus => focus.Bone, StringComparer.Ordinal)
                        .ToList();
                    foreach (Runner.FocusBoneSummary focus in focusSummaries)
                    {
                        builder.AppendLine("    {");
                        WriteJsonProperty(builder, 3, "bone", focus.Bone, comma: true);
                        WriteJsonProperty(builder, 3, "row_count", focus.RowCount, comma: true);
                        WriteJsonProperty(builder, 3, "max_rotation_difference_degrees", focus.MaxRotationDifferenceDegrees, comma: true);
                        WriteJsonProperty(builder, 3, "max_rotation_frame", focus.MaxRotationFrame, comma: true);
                        WriteJsonProperty(builder, 3, "high_rotation_difference_count", focus.HighRotationDifferenceCount, comma: false);
                        builder.Append("    }");
                        builder.AppendLine(++focusIndex == focusSummaries.Count ? "" : ",");
                    }
                    builder.AppendLine("  ],");

                    builder.AppendLine("  \"top_rows\": [");
                    for (int i = 0; i < summary.TopRows.Count; i++)
                    {
                        Runner.PoseComparisonRow row = summary.TopRows[i];
                        builder.AppendLine("    {");
                        WriteJsonProperty(builder, 3, "frame", row.Frame, comma: true);
                        WriteJsonProperty(builder, 3, "time_seconds", row.TimeSeconds, comma: true);
                        WriteJsonProperty(builder, 3, "bone", row.Bone, comma: true);
                        WriteJsonProperty(builder, 3, "rotation_difference_degrees", row.RotationDifferenceDegrees, comma: false);
                        builder.Append("    }");
                        builder.AppendLine(i == summary.TopRows.Count - 1 ? "" : ",");
                    }
                    builder.AppendLine("  ],");
                    builder.AppendLine("  \"import_variant_summary\": {");
                    WriteJsonProperty(builder, 2, "variant_count", variantCorrelation.VariantCount, comma: true);
                    WriteJsonProperty(builder, 2, "primary_like_count", variantCorrelation.PrimaryLikeCount, comma: true);
                    WriteJsonProperty(builder, 2, "fallback_like_count", variantCorrelation.FallbackLikeCount, comma: true);
                    WriteJsonProperty(builder, 2, "mixed_or_neutral_count", variantCorrelation.MixedOrNeutralCount, comma: false);
                    builder.AppendLine("  },");
                    builder.AppendLine("  \"import_variants\": [");
                    for (int i = 0; i < importVariants.Count; i++)
                    {
                        WriteImportVariantJson(builder, importVariants[i], i == importVariants.Count - 1);
                    }
                    builder.AppendLine("  ],");
                    WriteRuntimeImporterComparisonJson(builder, runtimeImporterComparison, reportDirectory, projectRoot);
                    builder.AppendLine("}");
                    File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
                }

                private static void WriteRuntimeImporterComparisonJson(
                    StringBuilder builder,
                    Runner.RuntimeImporterComparison comparison,
                    string reportDirectory,
                    string projectRoot)
                {
                    string runtimeRowsCsvPath = Path.Combine(reportDirectory, "runtime_importer_rows.csv");
                    string runtimeVmdSamplesCsvPath = Path.Combine(reportDirectory, "runtime_importer_vmd_samples.csv");
                    string rawAssimpRowsCsvPath = Path.Combine(reportDirectory, "raw_assimp_vs_runtime_importer_rows.csv");
                    string rawAssimpVmdSamplesCsvPath = Path.Combine(reportDirectory, "raw_assimp_vmd_samples.csv");
                    WriteRowsCsv(runtimeRowsCsvPath, comparison.Rows);
                    File.WriteAllText(runtimeVmdSamplesCsvPath, BuildRuntimeImporterVmdSampleCsv(comparison.VmdSampleRows), Encoding.UTF8);
                    WriteRowsCsv(rawAssimpRowsCsvPath, comparison.RawAssimpRows);
                    File.WriteAllText(rawAssimpVmdSamplesCsvPath, BuildRuntimeImporterVmdSampleCsv(comparison.RawAssimpVmdSampleRows), Encoding.UTF8);

                    builder.AppendLine("  \"runtime_importer_comparison\": {");
                    WriteJsonProperty(builder, 2, "asset_path", comparison.AssetPath, comma: true);
                    WriteJsonProperty(builder, 2, "runtime_clip_name", comparison.RuntimeClipName, comma: true);
                    WriteJsonProperty(builder, 2, "rows_csv_path", MakeProjectRelativePath(projectRoot, runtimeRowsCsvPath), comma: true);
                    WriteJsonProperty(builder, 2, "vmd_samples_csv_path", MakeProjectRelativePath(projectRoot, runtimeVmdSamplesCsvPath), comma: true);
                    WriteJsonProperty(builder, 2, "vmd_sample_count", comparison.VmdSampleRows.Count, comma: true);
                    builder.AppendLine("    \"summary\": {");
                    WriteSummaryJson(builder, comparison.Summary, 3);
                    builder.AppendLine("    },");
                    builder.AppendLine("    \"top_rows\": [");
                    for (int i = 0; i < comparison.Summary.TopRows.Count; i++)
                    {
                        Runner.PoseComparisonRow row = comparison.Summary.TopRows[i];
                        builder.AppendLine("      {");
                        WriteJsonProperty(builder, 4, "frame", row.Frame, comma: true);
                        WriteJsonProperty(builder, 4, "time_seconds", row.TimeSeconds, comma: true);
                        WriteJsonProperty(builder, 4, "bone", row.Bone, comma: true);
                        WriteJsonProperty(builder, 4, "rotation_difference_degrees", row.RotationDifferenceDegrees, comma: false);
                        builder.Append("      }");
                        builder.AppendLine(i == comparison.Summary.TopRows.Count - 1 ? "" : ",");
                    }

                    builder.AppendLine("    ],");
                    builder.AppendLine("    \"raw_assimp_channel_comparison\": {");
                    WriteJsonProperty(builder, 3, "animation_name", comparison.RawAssimpAnimationName, comma: true);
                    WriteJsonProperty(builder, 3, "rows_csv_path", MakeProjectRelativePath(projectRoot, rawAssimpRowsCsvPath), comma: true);
                    WriteJsonProperty(builder, 3, "vmd_samples_csv_path", MakeProjectRelativePath(projectRoot, rawAssimpVmdSamplesCsvPath), comma: true);
                    WriteJsonProperty(builder, 3, "vmd_sample_count", comparison.RawAssimpVmdSampleRows.Count, comma: true);
                    builder.AppendLine("      \"summary\": {");
                    WriteSummaryJson(builder, comparison.RawAssimpSummary, 4);
                    builder.AppendLine("      },");
                    builder.AppendLine("      \"top_rows\": [");
                    for (int i = 0; i < comparison.RawAssimpSummary.TopRows.Count; i++)
                    {
                        Runner.PoseComparisonRow row = comparison.RawAssimpSummary.TopRows[i];
                        builder.AppendLine("        {");
                        WriteJsonProperty(builder, 5, "frame", row.Frame, comma: true);
                        WriteJsonProperty(builder, 5, "time_seconds", row.TimeSeconds, comma: true);
                        WriteJsonProperty(builder, 5, "bone", row.Bone, comma: true);
                        WriteJsonProperty(builder, 5, "rotation_difference_degrees", row.RotationDifferenceDegrees, comma: false);
                        builder.Append("        }");
                        builder.AppendLine(i == comparison.RawAssimpSummary.TopRows.Count - 1 ? "" : ",");
                    }

                    builder.AppendLine("      ],");
                    WriteRawAssimpImportVariantsJson(
                        builder,
                        comparison.RawAssimpImportVariantSummary,
                        comparison.RawAssimpImportVariants,
                        reportDirectory,
                        projectRoot);
                    builder.AppendLine("    }");
                    builder.AppendLine("  }");
                }

                private static void WriteRawAssimpImportVariantsJson(
                    StringBuilder builder,
                    Runner.RawAssimpImportVariantSummary summary,
                    IReadOnlyList<Runner.RawAssimpImportVariantComparison> variants,
                    string reportDirectory,
                    string projectRoot)
                {
                    string variantDirectory = Path.Combine(reportDirectory, "raw_assimp_import_variants");
                    Directory.CreateDirectory(variantDirectory);
                    builder.AppendLine("      \"import_variant_summary\": {");
                    WriteJsonProperty(builder, 4, "variant_count", summary.VariantCount, comma: true);
                    WriteJsonProperty(builder, 4, "default_like_count", summary.DefaultLikeCount, comma: true);
                    WriteJsonProperty(builder, 4, "changed_count", summary.ChangedCount, comma: true);
                    WriteJsonProperty(builder, 4, "max_changed_variant_name", summary.MaxChangedVariantName, comma: true);
                    WriteJsonProperty(builder, 4, "max_changed_rotation_degrees", summary.MaxChangedRotationDegrees, comma: false);
                    builder.AppendLine("      },");
                    builder.AppendLine("      \"import_variants\": [");
                    for (int i = 0; i < variants.Count; i++)
                    {
                        Runner.RawAssimpImportVariantComparison variant = variants[i];
                        string rowsPath = Path.Combine(variantDirectory, variant.VariantName + "_rows.csv");
                        string vmdSamplesPath = Path.Combine(variantDirectory, variant.VariantName + "_vmd_samples.csv");
                        WriteRowsCsv(rowsPath, variant.Rows);
                        File.WriteAllText(vmdSamplesPath, BuildRuntimeImporterVmdSampleCsv(variant.VmdSampleRows), Encoding.UTF8);

                        builder.AppendLine("        {");
                        WriteJsonProperty(builder, 5, "variant_name", variant.VariantName, comma: true);
                        WriteJsonProperty(builder, 5, "preserve_pivots", variant.PreservePivots, comma: true);
                        WriteJsonProperty(builder, 5, "post_process_label", variant.PostProcessLabel, comma: true);
                        WriteJsonProperty(builder, 5, "animation_name", variant.AnimationName, comma: true);
                        WriteJsonProperty(builder, 5, "rows_csv_path", MakeProjectRelativePath(projectRoot, rowsPath), comma: true);
                        WriteJsonProperty(builder, 5, "vmd_samples_csv_path", MakeProjectRelativePath(projectRoot, vmdSamplesPath), comma: true);
                        WriteJsonProperty(builder, 5, "vmd_sample_count", variant.VmdSampleRows.Count, comma: true);
                        builder.AppendLine("          \"comparison_to_default\": {");
                        WriteSummaryJson(builder, variant.ComparisonToDefault, 6);
                        builder.AppendLine("          }");
                        builder.Append("        }");
                        builder.AppendLine(i == variants.Count - 1 ? "" : ",");
                    }

                    builder.AppendLine("      ]");
                }

                internal static string BuildRuntimeImporterVmdSampleCsv(IEnumerable<Runner.RuntimeImporterVmdSampleRow> rows)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("frameNumber,boneName,boneIndex,sourceMode,exportSourceMode,ghostVsSourceLocalDeltaDegrees,parentRestBasisCorrectedVsSourceLocalDeltaDegrees,exportVsSourceLocalDeltaDegrees,sourceLocalDeltaX,sourceLocalDeltaY,sourceLocalDeltaZ,sourceLocalDeltaW,exportLocalX,exportLocalY,exportLocalZ,exportLocalW,exportVmdX,exportVmdY,exportVmdZ,exportVmdW,humanBone,timeSeconds");
                    foreach (Runner.RuntimeImporterVmdSampleRow row in rows)
                    {
                        builder.Append(row.Frame.ToString(CultureInfo.InvariantCulture));
                        builder.Append(',');
                        builder.Append(EscapeCsv(row.VmdBoneName));
                        builder.Append(',');
                        builder.Append(row.BoneIndex.ToString(CultureInfo.InvariantCulture));
                        builder.Append(',');
                        builder.Append(EscapeCsv(string.IsNullOrWhiteSpace(row.SourceMode) ? "runtime_importer_local" : row.SourceMode));
                        builder.Append(',');
                        builder.Append(EscapeCsv(string.IsNullOrWhiteSpace(row.ExportSourceMode) ? "flip_xz_runtime_importer_local" : row.ExportSourceMode));
                        builder.Append(",,,,");
                        AppendQuaternion(builder, row.LocalRotation);
                        builder.Append(',');
                        AppendQuaternion(builder, row.LocalRotation);
                        builder.Append(',');
                        AppendQuaternion(builder, row.ExportVmdRotation);
                        builder.Append(',');
                        builder.Append(EscapeCsv(row.HumanBone));
                        builder.Append(',');
                        builder.Append(F(row.TimeSeconds));
                        builder.AppendLine();
                    }

                    return builder.ToString();
                }

                private static void AppendQuaternion(StringBuilder builder, Quaternion value)
                {
                    builder.Append(F(value.x));
                    builder.Append(',');
                    builder.Append(F(value.y));
                    builder.Append(',');
                    builder.Append(F(value.z));
                    builder.Append(',');
                    builder.Append(F(value.w));
                }

                internal static Quaternion ConvertUnityRotationToVmdRotation(Quaternion unityRotation)
                {
                    return new Quaternion(-unityRotation.x, unityRotation.y, -unityRotation.z, unityRotation.w);
                }

                private static void WriteImportVariantJson(StringBuilder builder, Runner.ImportVariantComparison comparison, bool last)
                {
                    builder.AppendLine("    {");
                    WriteJsonProperty(builder, 3, "variant_name", comparison.VariantName, comma: true);
                    WriteJsonProperty(builder, 3, "asset_path", comparison.AssetPath, comma: true);
                    WriteJsonProperty(builder, 3, "clip_name", comparison.ClipName, comma: true);
                    WriteJsonProperty(builder, 3, "clip_length_seconds", comparison.ClipLengthSeconds, comma: true);
                    builder.AppendLine("      \"comparison_to_primary\": {");
                    WriteSummaryJson(builder, comparison.ComparisonToPrimary, 4);
                    builder.AppendLine("      },");
                    builder.AppendLine("      \"comparison_to_fallback\": {");
                    WriteSummaryJson(builder, comparison.ComparisonToFallback, 4);
                    builder.AppendLine("      }");
                    builder.Append("    }");
                    builder.AppendLine(last ? "" : ",");
                }

                private static void WriteSummaryJson(StringBuilder builder, Runner.ClipComparisonSummary summary, int indent)
                {
                    WriteJsonProperty(builder, indent, "row_count", summary.RowCount, comma: true);
                    WriteJsonProperty(builder, indent, "high_rotation_difference_count", summary.HighRotationDifferenceCount, comma: true);
                    WriteJsonProperty(builder, indent, "max_rotation_difference_degrees", summary.MaxRotationDifferenceDegrees, comma: true);
                    WriteJsonProperty(builder, indent, "max_rotation_bone", summary.MaxRotationBone, comma: true);
                    WriteJsonProperty(builder, indent, "max_rotation_frame", summary.MaxRotationFrame, comma: false);
                }

                private static void WriteJsonProperty(StringBuilder builder, int indent, string name, string value, bool comma)
                {
                    builder.Append(' ', indent * 2);
                    builder.Append('"').Append(EscapeJson(name)).Append("\": \"").Append(EscapeJson(value)).Append('"');
                    builder.AppendLine(comma ? "," : "");
                }

                private static void WriteJsonProperty(StringBuilder builder, int indent, string name, int value, bool comma)
                {
                    builder.Append(' ', indent * 2);
                    builder.Append('"').Append(EscapeJson(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
                    builder.AppendLine(comma ? "," : "");
                }

                private static void WriteJsonProperty(StringBuilder builder, int indent, string name, float value, bool comma)
                {
                    builder.Append(' ', indent * 2);
                    builder.Append('"').Append(EscapeJson(name)).Append("\": ").Append(F(value));
                    builder.AppendLine(comma ? "," : "");
                }

                private static void WriteJsonProperty(StringBuilder builder, int indent, string name, bool value, bool comma)
                {
                    builder.Append(' ', indent * 2);
                    builder.Append('"').Append(EscapeJson(name)).Append("\": ").Append(value ? "true" : "false");
                    builder.AppendLine(comma ? "," : "");
                }

                private static void WriteJsonArray(StringBuilder builder, int indent, string name, IEnumerable<string> values, bool comma)
                {
                    builder.Append(' ', indent * 2);
                    builder.Append('"').Append(EscapeJson(name)).Append("\": [");
                    builder.Append(string.Join(", ", values));
                    builder.Append(']');
                    builder.AppendLine(comma ? "," : "");
                }

                private static string Sha256File(string path)
                {
                    using (SHA256 sha = SHA256.Create())
                    using (FileStream stream = File.OpenRead(path))
                    {
                        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                    }
                }

                private static string ReplaceGuid(string meta, string guid)
                {
                    return Regex.Replace(
                        meta,
                        @"(?m)^guid:\s*[0-9a-fA-F]+",
                        "guid: " + guid,
                        RegexOptions.CultureInvariant);
                }

                private static string ReplaceScalar(string meta, string key, string value)
                {
                    string pattern = @"(?m)^(\s*" + Regex.Escape(key) + @":\s*).*$";
                    string replaced = Regex.Replace(
                        meta,
                        pattern,
                        "${1}" + value,
                        RegexOptions.CultureInvariant);
                    if (string.Equals(replaced, meta, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Scalar key not found in meta: {key}");
                    }

                    return replaced;
                }

                private static string ExtractRequiredScalar(string meta, string key)
                {
                    Match match = Regex.Match(
                        meta,
                        @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$",
                        RegexOptions.CultureInvariant);
                    if (!match.Success)
                    {
                        throw new InvalidOperationException($"Scalar key not found in source meta: {key}");
                    }

                    return match.Groups[1].Value;
                }

                private static string ReplaceYamlBlock(string targetMeta, string sourceMeta, string key)
                {
                    List<string> targetLines = SplitLines(targetMeta);
                    List<string> sourceLines = SplitLines(sourceMeta);
                    BlockRange targetRange = FindYamlBlock(targetLines, key);
                    BlockRange sourceRange = FindYamlBlock(sourceLines, key);

                    var result = new List<string>();
                    result.AddRange(targetLines.Take(targetRange.Start));
                    result.AddRange(sourceLines.Skip(sourceRange.Start).Take(sourceRange.End - sourceRange.Start));
                    result.AddRange(targetLines.Skip(targetRange.End));
                    return string.Join("\n", result) + "\n";
                }

                private static BlockRange FindYamlBlock(IReadOnlyList<string> lines, string key)
                {
                    for (int index = 0; index < lines.Count; index++)
                    {
                        string line = lines[index];
                        if (!IsYamlKeyLine(line, key))
                        {
                            continue;
                        }

                        int startIndent = CountIndent(line);
                        int end = index + 1;
                        while (end < lines.Count)
                        {
                            string candidate = lines[end];
                            string trimmedCandidate = candidate.TrimStart();
                            if (
                                !string.IsNullOrWhiteSpace(candidate)
                                && CountIndent(candidate) <= startIndent
                                && !trimmedCandidate.StartsWith("-", StringComparison.Ordinal))
                            {
                                break;
                            }

                            end++;
                        }

                        return new BlockRange(index, end);
                    }

                    throw new InvalidOperationException($"YAML block not found: {key}");
                }

                private static bool IsYamlKeyLine(string line, string key)
                {
                    string trimmed = line.TrimStart();
                    return trimmed.StartsWith(key + ":", StringComparison.Ordinal);
                }

                private static int CountIndent(string line)
                {
                    int count = 0;
                    while (count < line.Length && line[count] == ' ')
                    {
                        count++;
                    }

                    return count;
                }

                private static List<string> SplitLines(string text)
                {
                    return text
                        .Replace("\r\n", "\n")
                        .Replace('\r', '\n')
                        .TrimEnd('\n')
                        .Split('\n')
                        .ToList();
                }

                private readonly struct BlockRange
                {
                    public BlockRange(int start, int end)
                    {
                        Start = start;
                        End = end;
                    }

                    public int Start { get; }
                    public int End { get; }
                }

                internal static string MakeProjectRelativePath(string projectRoot, string path)
                {
                    string fullPath = Path.GetFullPath(path).Replace('\\', '/');
                    string fullRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
                    if (fullPath.StartsWith(fullRoot + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return fullPath.Substring(fullRoot.Length + 1);
                    }

                    return fullPath;
                }

                private static string EscapeJson(string value)
                {
                    if (value == null)
                    {
                        return string.Empty;
                    }

                    return value
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t");
                }

                private static string EscapeCsv(string value)
                {
                    if (value == null)
                    {
                        return string.Empty;
                    }

                    if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                    {
                        return value;
                    }

                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }

                private static string F(float value)
                {
                    return value.ToString("0.######", CultureInfo.InvariantCulture);
                }

                private static string V(Vector3 value)
                {
                    return $"{F(value.x)} {F(value.y)} {F(value.z)}";
                }
    }
}
#endif
