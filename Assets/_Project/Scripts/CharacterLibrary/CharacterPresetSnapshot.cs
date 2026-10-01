using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using UnityEngine;
using VRM;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 프리셋의 캡처/적용 코덱. [SerializeField] 원시 필드(bool/int/float/string/
    /// Vector3/enum)를 리플렉션으로 읽고 쓴다. 참조형 필드는 자동으로 제외돼
    /// 프리셋이 씬 객체를 잘못 연결하지 않는다.
    /// 섹션별 키 규칙:
    ///   retarget   = FBXVmdPipeline 필드명
    ///   placement  = "position"|"rotation"|"scale" (캐릭터 루트), "guard.&lt;필드&gt;" (유휴 가드)
    ///   physics    = "spring[&lt;i&gt;].&lt;필드&gt;" (VRMSpringBone 컴포넌트 순서)
    ///   appearance = 예약
    /// </summary>
    public static class CharacterPresetSnapshot
    {
        private const string GuardKeyPrefix = "guard.";
        private const string SpringKeyPrefix = "spring[";

        // retarget 섹션의 대상은 리타깃/배치 튜닝 필드다. 출력 경로·녹화·진단·
        // 로그 같은 파이프라인 전역 설정은 캐릭터 프리셋에 포함하지 않는다.
        private static readonly string[] RetargetExcludedSubstrings =
        {
            "Folder", "Output", "Path", "File",
            "Video", "Recording", "Diagnostic",
            "Smoke", "_log", "_show", "_debug",
            "StartDelay", "PlaybackSpeed",
        };

        // VRMSpringBone에서 프리셋 대상으로 삼는 필드(파일 포맷에 고정).
        private static readonly string[] SpringBoneFields =
        {
            "m_stiffnessForce",
            "m_gravityPower",
            "m_gravityDir",
            "m_dragForce",
            "m_hitRadius",
            "m_updateType",
        };

        public static CharacterPreset CreateEmpty(string name)
        {
            string now = DateTime.UtcNow.ToString("O");
            return new CharacterPreset
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(name) ? "프리셋" : name.Trim(),
                createdAtUtc = now,
                updatedAtUtc = now,
            };
        }

        /// <summary>
        /// 파이프라인의 직렬화 원시 필드를 retarget 섹션에 기록한다.
        /// </summary>
        public static void CaptureRetarget(CharacterPreset preset, FBXVmdPipeline pipeline)
        {
            preset.sections.retarget.Clear();
            if (pipeline == null)
            {
                return;
            }
            CaptureFields(pipeline, string.Empty, preset.sections.retarget,
                IsRetargetFieldExcluded);
        }

        /// <summary>
        /// 캐릭터 루트의 배치와 유휴 가드 플래그를 placement 섹션에 기록한다.
        /// </summary>
        public static void CapturePlacement(
            CharacterPreset preset, GameObject characterRoot, TargetIdlePoseGuard guard)
        {
            preset.sections.placement.Clear();
            if (characterRoot != null)
            {
                Transform t = characterRoot.transform;
                preset.sections.placement.Add(Encode("position", t.position));
                preset.sections.placement.Add(Encode("rotation", t.rotation));
                preset.sections.placement.Add(Encode("scale", t.localScale));
            }
            if (guard != null)
            {
                CaptureFields(guard, GuardKeyPrefix, preset.sections.placement);
            }
        }

        /// <summary>
        /// VRMSpringBone 컴포넌트들의 스프링 파라미터를 physics 섹션에 기록한다.
        /// 인덱스 키라 같은 모델 파일에서만 의미가 있다(캐릭터 귀속 프리셋이므로 성립).
        /// </summary>
        public static void CapturePhysics(CharacterPreset preset, GameObject characterRoot)
        {
            preset.sections.physics.Clear();
            if (characterRoot == null)
            {
                return;
            }

            VRMSpringBone[] springs = characterRoot.GetComponentsInChildren<VRMSpringBone>(true);
            for (int i = 0; i < springs.Length; i++)
            {
                for (int f = 0; f < SpringBoneFields.Length; f++)
                {
                    FieldInfo field = typeof(VRMSpringBone).GetField(
                        SpringBoneFields[f],
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field == null)
                    {
                        continue;
                    }
                    if (TryEncode(field.GetValue(springs[i]), field.FieldType,
                            out string type, out string value))
                    {
                        preset.sections.physics.Add(new PresetFieldValue
                        {
                            key = $"spring[{i}].{SpringBoneFields[f]}",
                            type = type,
                            value = value,
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 프리셋을 파이프라인/캐릭터 루트/가드에 적용한다.
        /// 없는 필드·해석 실패 값은 건너뛰고 경고 목록에 모은다.
        /// </summary>
        public static List<string> Apply(
            CharacterPreset preset,
            FBXVmdPipeline pipeline,
            GameObject characterRoot,
            TargetIdlePoseGuard guard)
        {
            var warnings = new List<string>();
            if (preset == null || preset.sections == null)
            {
                return warnings;
            }

#if UNITY_EDITOR
            // 에디트 모드 적용은 되돌리기(Ctrl+Z)가 가능하게 대상을 Undo에 기록한다.
            if (!Application.isPlaying)
            {
                var undoTargets = new List<UnityEngine.Object>();
                if (pipeline != null)
                {
                    undoTargets.Add(pipeline);
                }
                if (characterRoot != null)
                {
                    undoTargets.Add(characterRoot.transform);
                    undoTargets.AddRange(
                        characterRoot.GetComponentsInChildren<VRMSpringBone>(true));
                }
                if (guard != null)
                {
                    undoTargets.Add(guard);
                }
                if (undoTargets.Count > 0)
                {
                    UnityEditor.Undo.RecordObjects(
                        undoTargets.ToArray(), "캐릭터 프리셋 적용");
                }
            }
#endif

            if (pipeline != null)
            {
                ApplyFields(pipeline, string.Empty, preset.sections.retarget, warnings);
            }

            ApplyPlacement(preset.sections.placement, characterRoot, guard, warnings);
            ApplyPhysics(preset.sections.physics, characterRoot, warnings);

            // placement가 루트 변환을 바꿨으면 유휴 가드의 베이스라인을 재캡처한다.
            // 그렇지 않으면 Play 중 다음 프레임에 가드가 캐릭터를 이전 자세로 되돌린다.
            if (pipeline != null && preset.sections.placement != null &&
                preset.sections.placement.Count > 0)
            {
                pipeline.RebindIdlePoseGuard();
            }
            // appearance 섹션은 예약 — 읽기만 하고 적용하지 않는다.
            return warnings;
        }

        private static void ApplyPlacement(
            List<PresetFieldValue> values, GameObject root,
            TargetIdlePoseGuard guard, List<string> warnings)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                PresetFieldValue field = values[i];
                if (field.key.StartsWith(GuardKeyPrefix, StringComparison.Ordinal))
                {
                    if (guard != null)
                    {
                        ApplyFieldValue(
                            guard, field.key.Substring(GuardKeyPrefix.Length), field, warnings);
                    }
                    else
                    {
                        warnings.Add($"유휴 가드가 없어 건너뜀: {field.key}");
                    }
                    continue;
                }

                if (root == null)
                {
                    continue;
                }

                if (field.key == "position" && TryDecode(field, out Vector3 position))
                {
                    root.transform.position = position;
                }
                else if (field.key == "rotation" && TryDecode(field, out Quaternion rotation))
                {
                    root.transform.rotation = rotation;
                }
                else if (field.key == "scale" && TryDecode(field, out Vector3 scale))
                {
                    root.transform.localScale = scale;
                }
            }
        }

        private static void ApplyPhysics(
            List<PresetFieldValue> values, GameObject root, List<string> warnings)
        {
            if (values == null || values.Count == 0 || root == null)
            {
                return;
            }

            VRMSpringBone[] springs = root.GetComponentsInChildren<VRMSpringBone>(true);
            for (int i = 0; i < values.Count; i++)
            {
                PresetFieldValue field = values[i];
                int open = field.key.IndexOf('[');
                int close = field.key.IndexOf(']');
                int dot = field.key.IndexOf('.', close < 0 ? 0 : close);
                if (open < 0 || close <= open || dot <= close ||
                    !field.key.StartsWith(SpringKeyPrefix, StringComparison.Ordinal) ||
                    !int.TryParse(field.key.Substring(open + 1, close - open - 1),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    warnings.Add($"알 수 없는 물리 키: {field.key}");
                    continue;
                }

                if (index < 0 || index >= springs.Length)
                {
                    warnings.Add($"스프링 인덱스 범위 밖: {field.key}");
                    continue;
                }

                string fieldName = field.key.Substring(dot + 1);
                ApplyFieldValue(springs[index], fieldName, field, warnings);
            }
        }

        /// <summary>
        /// 대상 객체의 [SerializeField] 원시 필드를 값 목록에 기록한다.
        /// </summary>
        private static void CaptureFields(
            object target, string keyPrefix, List<PresetFieldValue> output,
            Func<string, bool> excludeField = null)
        {
            FieldInfo[] fields = target.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsDefined(typeof(SerializeField), inherit: true))
                {
                    continue;
                }
                if (excludeField != null && excludeField(field.Name))
                {
                    continue;
                }
                if (TryEncode(field.GetValue(target), field.FieldType,
                        out string type, out string value))
                {
                    output.Add(new PresetFieldValue
                    {
                        key = keyPrefix + field.Name,
                        type = type,
                        value = value,
                    });
                }
            }
        }

        private static void ApplyFields(
            object target, string keyPrefix, List<PresetFieldValue> values, List<string> warnings)
        {
            if (values == null || target == null)
            {
                return;
            }
            for (int i = 0; i < values.Count; i++)
            {
                PresetFieldValue field = values[i];
                string name = string.IsNullOrEmpty(keyPrefix)
                    ? field.key
                    : field.key.StartsWith(keyPrefix, StringComparison.Ordinal)
                        ? field.key.Substring(keyPrefix.Length)
                        : field.key;
                ApplyFieldValue(target, name, field, warnings);
            }
        }

        private static void ApplyFieldValue(
            object target, string fieldName, PresetFieldValue field, List<string> warnings)
        {
            FieldInfo info = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null || !info.IsDefined(typeof(SerializeField), inherit: true))
            {
                warnings.Add($"필드 없음: {fieldName}");
                return;
            }
            // 저장된 enum 타입명과 현재 필드 타입이 다르면 정수 매핑이 어긋나므로 건너뛴다.
            if (info.FieldType.IsEnum &&
                field.type.StartsWith(PresetFieldType.EnumPrefix, StringComparison.Ordinal))
            {
                string storedType = field.type.Substring(PresetFieldType.EnumPrefix.Length);
                if (!string.Equals(storedType, info.FieldType.FullName, StringComparison.Ordinal))
                {
                    warnings.Add(
                        $"enum 타입 불일치: {fieldName} ({storedType} != {info.FieldType.FullName})");
                    return;
                }
            }
            if (!TryDecodeFor(field, info.FieldType, out object value))
            {
                warnings.Add($"값 해석 실패: {fieldName}={field.value}");
                return;
            }
            try
            {
                info.SetValue(target, value);
            }
            catch (Exception)
            {
                warnings.Add($"타입 불일치: {fieldName}");
            }
        }

        private static bool TryEncode(object raw, Type fieldType, out string type, out string value)
        {
            type = string.Empty;
            value = string.Empty;

            if (fieldType == typeof(bool))
            {
                type = PresetFieldType.Bool;
                value = (bool)raw ? "true" : "false";
            }
            else if (fieldType == typeof(int))
            {
                type = PresetFieldType.Int;
                value = ((int)raw).ToString(CultureInfo.InvariantCulture);
            }
            else if (fieldType == typeof(float))
            {
                type = PresetFieldType.Float;
                value = ((float)raw).ToString("R", CultureInfo.InvariantCulture);
            }
            else if (fieldType == typeof(string))
            {
                type = PresetFieldType.String;
                value = (string)raw ?? string.Empty;
            }
            else if (fieldType == typeof(Vector3))
            {
                type = PresetFieldType.Vector3;
                Vector3 v = (Vector3)raw;
                value = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}", v.x, v.y, v.z);
            }
            else if (fieldType == typeof(Quaternion))
            {
                type = PresetFieldType.Quaternion;
                Quaternion q = (Quaternion)raw;
                value = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}",
                    q.x, q.y, q.z, q.w);
            }
            else if (fieldType.IsEnum)
            {
                type = PresetFieldType.EnumPrefix + fieldType.FullName;
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                return false;
            }
            return true;
        }

        private static bool TryDecodeFor(PresetFieldValue field, Type fieldType, out object value)
        {
            value = null;
            if (field.type == PresetFieldType.Bool)
            {
                if (bool.TryParse(field.value, out bool b)) { value = b; return true; }
            }
            else if (field.type == PresetFieldType.Int)
            {
                if (int.TryParse(field.value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int n)) { value = n; return true; }
            }
            else if (field.type == PresetFieldType.Float)
            {
                if (float.TryParse(field.value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float f)) { value = f; return true; }
            }
            else if (field.type == PresetFieldType.String)
            {
                value = field.value;
                return true;
            }
            else if (field.type == PresetFieldType.Vector3)
            {
                if (TryDecode(field, out Vector3 v)) { value = v; return true; }
            }
            else if (field.type == PresetFieldType.Quaternion)
            {
                if (TryDecode(field, out Quaternion q)) { value = q; return true; }
            }
            else if (field.type.StartsWith(PresetFieldType.EnumPrefix, StringComparison.Ordinal) &&
                     fieldType.IsEnum)
            {
                if (int.TryParse(field.value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int e))
                {
                    value = Enum.ToObject(fieldType, e);
                    return true;
                }
            }
            return false;
        }

        private static bool IsRetargetFieldExcluded(string fieldName)
        {
            for (int i = 0; i < RetargetExcludedSubstrings.Length; i++)
            {
                if (fieldName.IndexOf(RetargetExcludedSubstrings[i],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static PresetFieldValue Encode(string key, Vector3 v)
        {
            return new PresetFieldValue
            {
                key = key,
                type = PresetFieldType.Vector3,
                value = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}", v.x, v.y, v.z),
            };
        }

        private static PresetFieldValue Encode(string key, Quaternion q)
        {
            return new PresetFieldValue
            {
                key = key,
                type = PresetFieldType.Quaternion,
                value = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}",
                    q.x, q.y, q.z, q.w),
            };
        }

        private static bool TryDecode(PresetFieldValue field, out Vector3 v)
        {
            v = default;
            if (field.type != PresetFieldType.Vector3)
            {
                return false;
            }
            string[] parts = field.value.Split(',');
            if (parts.Length != 3 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return false;
            }
            v = new Vector3(x, y, z);
            return true;
        }

        private static bool TryDecode(PresetFieldValue field, out Quaternion q)
        {
            q = default;
            if (field.type != PresetFieldType.Quaternion)
            {
                return false;
            }
            string[] parts = field.value.Split(',');
            if (parts.Length != 4 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float w))
            {
                return false;
            }
            q = new Quaternion(x, y, z, w);
            return true;
        }
    }
}
