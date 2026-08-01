#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Modules.FBXImporter.EditorTools
{
    public static class FbxReferenceHipsLocalPositionProbeRunner
    {
        private const string MenuPath = "Machine Spirit/FBX Diagnostics/Run Reference Hips LocalPosition Probe";
        private const string EvidenceDirectory = "Docs/Workflow/Local/progress/evidence";
        private const string OutputCsv = "visual_body_arc_jitter_reference_hips_local_position_probe.csv";
        private const string OutputJson = "visual_body_arc_jitter_reference_hips_local_position_probe_manifest.json";
        private const string FbxClipPath = "Assets/_Project/FBX/satisfaction_2.fbx";
        private const string ImportFbxClipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string ManualControllerPath = "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller";
        private const string FallbackControllerPath = "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";
        private const string ManualYybPrefabPath = "Assets/_ManualReference/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_Prefab.prefab";
        private const string TestPrefabPath = "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab";
        private const float DurationSeconds = 31f;
        private const float FrameRate = 30f;

        [MenuItem(MenuPath, false, 2125)]
        public static void RunMenu()
        {
            string result = Run();
            Debug.Log(result);
        }

        public static string Run()
        {
            AnimationClip referenceClip = LoadFirstAnimationClip(FbxClipPath) ?? LoadFirstAnimationClip(ImportFbxClipPath);
            if (referenceClip == null)
            {
                throw new InvalidOperationException("satisfaction_2 기준 AnimationClip을 찾지 못했습니다.");
            }

            RuntimeAnimatorController fallbackController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ManualControllerPath) ??
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
            if (fallbackController == null)
            {
                throw new InvalidOperationException("수동 기준 Animator Controller를 찾지 못했습니다.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string evidenceDirectory = Path.Combine(projectRoot, EvidenceDirectory);
            Directory.CreateDirectory(evidenceDirectory);
            string csvPath = Path.Combine(evidenceDirectory, OutputCsv);
            string jsonPath = Path.Combine(evidenceDirectory, OutputJson);

            List<SampleVariant> variants = CollectSampleVariants(fallbackController);
            List<SampleRow> rows = new List<SampleRow>(variants.Count * Mathf.CeilToInt(DurationSeconds * FrameRate) * 2);
            List<string> failures = new List<string>();

            foreach (SampleVariant variant in variants)
            {
                SampleVariantMode(variant, referenceClip, fallbackController, applyRootMotion: false, rows, failures);
                SampleVariantMode(variant, referenceClip, fallbackController, applyRootMotion: true, rows, failures);
            }

            WriteCsv(csvPath, rows);
            WriteManifest(jsonPath, referenceClip, variants, rows.Count, failures);

            AssetDatabase.Refresh();
            return $"[ReferenceHipsLocalPositionProbe] rows={rows.Count}, variants={variants.Count}, csv={MakeProjectRelativePath(projectRoot, csvPath)}, json={MakeProjectRelativePath(projectRoot, jsonPath)}, failures={failures.Count}";
        }

        private static List<SampleVariant> CollectSampleVariants(RuntimeAnimatorController fallbackController)
        {
            List<SampleVariant> variants = new List<SampleVariant>();
            AddAssetVariant(variants, "manual_yyb_prefab", ManualYybPrefabPath, fallbackController);
            AddAssetVariant(variants, "testprefab_prefab", TestPrefabPath, fallbackController);

            foreach (Animator animator in UnityEngine.Object.FindObjectsOfType<Animator>(true))
            {
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    continue;
                }

                string path = GetHierarchyPath(animator.transform);
                string normalizedPath = SanitizeName(path);
                variants.Add(new SampleVariant(
                    $"scene_clone_{normalizedPath}",
                    animator.gameObject,
                    animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController : fallbackController,
                    "scene",
                    path));
            }

            return variants;
        }

        private static void AddAssetVariant(List<SampleVariant> variants, string name, string assetPath, RuntimeAnimatorController fallbackController)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return;
            }

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController controller = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController
                : fallbackController;
            variants.Add(new SampleVariant(name, prefab, controller, "asset", assetPath));
        }

        private static void SampleVariantMode(
            SampleVariant variant,
            AnimationClip referenceClip,
            RuntimeAnimatorController fallbackController,
            bool applyRootMotion,
            List<SampleRow> rows,
            List<string> failures)
        {
            GameObject instance = null;
            HumanPoseHandler poseHandler = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(variant.Source);
                instance.name = $"ReferenceHipsProbe_{variant.Name}_{(applyRootMotion ? "rootMotionOn" : "rootMotionOff")}";
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);

                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    failures.Add($"{variant.Name}: 유효한 Humanoid Animator 없음");
                    return;
                }

                RuntimeAnimatorController controller = variant.Controller != null ? variant.Controller : fallbackController;
                if (controller == null)
                {
                    failures.Add($"{variant.Name}: Animator Controller 없음");
                    return;
                }

                AnimatorOverrideController overrideController = new AnimatorOverrideController(controller);
                List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);
                string stateName = referenceClip.name;
                if (overrides.Count > 0 && overrides[0].Key != null)
                {
                    stateName = overrides[0].Key.name;
                    overrideController[overrides[0].Key] = referenceClip;
                }

                animator.runtimeAnimatorController = overrideController;
                animator.applyRootMotion = applyRootMotion;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.speed = 0f;
                animator.Rebind();
                animator.Update(0f);

                int stateHash = Animator.StringToHash(stateName);
                bool hasState = animator.HasState(0, stateHash);
                poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                HumanPose pose = new HumanPose();
                int frameCount = Mathf.CeilToInt(DurationSeconds * FrameRate);

                for (int frame = 0; frame < frameCount; frame++)
                {
                    float time = (frame + 1) / FrameRate;
                    float normalizedTime = Mathf.Clamp01(time / Mathf.Max(0.0001f, referenceClip.length));
                    if (hasState)
                    {
                        animator.Play(stateHash, 0, normalizedTime);
                    }
                    else
                    {
                        animator.Play(0, 0, normalizedTime);
                    }

                    animator.Update(0f);
                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    poseHandler.GetHumanPose(ref pose);
                    rows.Add(new SampleRow
                    {
                        Variant = variant.Name,
                        SourceKind = variant.SourceKind,
                        Source = variant.SourceDescription,
                        ApplyRootMotion = applyRootMotion,
                        Frame = frame + 1,
                        Time = time,
                        ControllerPath = AssetDatabase.GetAssetPath(controller),
                        ClipName = referenceClip.name,
                        StateName = stateName,
                        HasState = hasState,
                        RootPosition = animator.transform.position,
                        RootRotationEuler = animator.transform.rotation.eulerAngles,
                        RootLocalScale = animator.transform.localScale,
                        HipsLocalPosition = hips != null ? hips.localPosition : MissingVector(),
                        HipsWorldPosition = hips != null ? hips.position : MissingVector(),
                        HumanBodyPosition = pose.bodyPosition,
                        HumanBodyRotationEuler = pose.bodyRotation.eulerAngles
                    });
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{variant.Name}: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (poseHandler != null)
                {
                    poseHandler.Dispose();
                }

                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static AnimationClip LoadFirstAnimationClip(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static void WriteCsv(string path, List<SampleRow> rows)
        {
            StringBuilder builder = new StringBuilder(rows.Count * 240);
            builder.AppendLine("variant,sourceKind,source,applyRootMotion,frame,time,controllerPath,clipName,stateName,hasState,rootPos,rootRotEuler,rootLocalScale,hipsLocalPos,hipsWorldPos,humanBodyPos,humanBodyRotEuler");
            foreach (SampleRow row in rows)
            {
                builder.AppendLine(string.Join(",", new[]
                {
                    Escape(row.Variant),
                    Escape(row.SourceKind),
                    Escape(row.Source),
                    row.ApplyRootMotion ? "true" : "false",
                    row.Frame.ToString(CultureInfo.InvariantCulture),
                    F(row.Time),
                    Escape(row.ControllerPath),
                    Escape(row.ClipName),
                    Escape(row.StateName),
                    row.HasState ? "true" : "false",
                    V(row.RootPosition),
                    V(row.RootRotationEuler),
                    V(row.RootLocalScale),
                    V(row.HipsLocalPosition),
                    V(row.HipsWorldPosition),
                    V(row.HumanBodyPosition),
                    V(row.HumanBodyRotationEuler)
                }));
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteManifest(string path, AnimationClip clip, List<SampleVariant> variants, int rowCount, List<string> failures)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"schema_version\": \"reference_hips_local_position_probe.v1\",");
            builder.AppendLine("  \"case_name\": \"reference_hips_local_position_probe\",");
            builder.AppendLine($"  \"generated_at\": \"{DateTime.Now:O}\",");
            builder.AppendLine($"  \"clip_name\": \"{EscapeJson(clip.name)}\",");
            builder.AppendLine($"  \"clip_length_seconds\": {F(clip.length)},");
            builder.AppendLine($"  \"duration_seconds\": {F(DurationSeconds)},");
            builder.AppendLine($"  \"frame_rate\": {F(FrameRate)},");
            builder.AppendLine($"  \"row_count\": {rowCount},");
            builder.AppendLine("  \"variants\": [");
            for (int i = 0; i < variants.Count; i++)
            {
                SampleVariant variant = variants[i];
                builder.AppendLine("    {");
                builder.AppendLine($"      \"name\": \"{EscapeJson(variant.Name)}\",");
                builder.AppendLine($"      \"source_kind\": \"{EscapeJson(variant.SourceKind)}\",");
                builder.AppendLine($"      \"source\": \"{EscapeJson(variant.SourceDescription)}\"");
                builder.Append("    }");
                builder.AppendLine(i == variants.Count - 1 ? "" : ",");
            }
            builder.AppendLine("  ],");
            builder.AppendLine("  \"failures\": [");
            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append($"    \"{EscapeJson(failures[i])}\"");
                builder.AppendLine(i == failures.Count - 1 ? "" : ",");
            }
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return builder.ToString().Trim('_');
        }

        private static Vector3 MissingVector()
        {
            return new Vector3(float.NaN, float.NaN, float.NaN);
        }

        private static string F(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 value)
        {
            return $"{F(value.x)}|{F(value.y)}|{F(value.z)}";
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string MakeProjectRelativePath(string projectRoot, string absolutePath)
        {
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(absolutePath);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("\\", "/");
            }

            return fullPath.Replace("\\", "/");
        }

        private sealed class SampleVariant
        {
            public SampleVariant(string name, GameObject source, RuntimeAnimatorController controller, string sourceKind, string sourceDescription)
            {
                Name = name;
                Source = source;
                Controller = controller;
                SourceKind = sourceKind;
                SourceDescription = sourceDescription;
            }

            public string Name { get; }
            public GameObject Source { get; }
            public RuntimeAnimatorController Controller { get; }
            public string SourceKind { get; }
            public string SourceDescription { get; }
        }

        private struct SampleRow
        {
            public string Variant;
            public string SourceKind;
            public string Source;
            public bool ApplyRootMotion;
            public int Frame;
            public float Time;
            public string ControllerPath;
            public string ClipName;
            public string StateName;
            public bool HasState;
            public Vector3 RootPosition;
            public Vector3 RootRotationEuler;
            public Vector3 RootLocalScale;
            public Vector3 HipsLocalPosition;
            public Vector3 HipsWorldPosition;
            public Vector3 HumanBodyPosition;
            public Vector3 HumanBodyRotationEuler;
        }
    }
}
#endif
