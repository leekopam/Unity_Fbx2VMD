"""FBX 발 궤적 덤프 — Blender headless에서 발목/발끝 월드 좌표를 프레임별 JSON으로 출력.

Unity 접지 의도 추정(HumanoidFootContactIntentEstimator)의 입력과 동일한 형태의
원본 궤적을 독립 경로로 샘플링해 정답지 비교에 사용한다.

실행:
    blender -b --factory-startup -P dump_foot_trajectory.py -- \
        --input <fbx경로> --output <json경로> [--fps 60]

출력 좌표계: 아마추어 로컬+오브젝트 변환 적용값. up_axis 필드가 상방 축을 알려주며
Unity 쪽에서는 해당 축을 y로 매핑해 사용한다(수평 두 축의 순서는 분석에 무의미).
"""

import bpy
import json
import re
import sys
import os


def parse_args():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []
    args = {}
    key = None
    for token in argv:
        if token.startswith("--"):
            key = token[2:]
        elif key:
            args[key] = token
            key = None
    return args


def side_of(name):
    """본 이름에서 좌/우를 판별한다."""
    lower = name.lower()
    if "left" in lower or "左" in name:
        return "l"
    if "right" in lower or "右" in name:
        return "r"
    if re.search(r"(^|[\W_])l([\W_]|$)", lower):
        return "l"
    if re.search(r"(^|[\W_])r([\W_]|$)", lower):
        return "r"
    return None


FOOT_HINTS = ("foot", "ankle", "足首")
TOE_HINTS = ("toe", "つま先")
EXCLUDE_HINTS = ("ik", "iktarget", "pole", "ＩＫ")


def classify_bone(name):
    """foot/toe 역할을 판별한다. IK·보조 본은 제외."""
    lower = name.lower()
    if any(hint in lower for hint in EXCLUDE_HINTS) or "ＩＫ" in name:
        return None
    if any(hint in lower for hint in TOE_HINTS) or "つま先" in name:
        return "toe"
    if any(hint in lower for hint in FOOT_HINTS) or "足首" in name:
        return "foot"
    return None


def pick_bones(armature):
    """좌/우 발목·발끝 본 이름을 자동 선택한다. 실패 항목은 warnings에 남긴다."""
    found = {"lf": None, "lt": None, "rf": None, "rt": None}
    warnings = []
    for bone in armature.data.bones:
        side = side_of(bone.name)
        role = classify_bone(bone.name)
        if side is None or role is None:
            continue
        key = side + ("f" if role == "foot" else "t")
        if found[key] is None:
            found[key] = bone.name
        else:
            warnings.append(f"{key} 후보 중복: {found[key]} / {bone.name} (첫 항목 사용)")
    for key, value in found.items():
        if value is None:
            warnings.append(f"{key} 본을 찾지 못함")
    # 상방 축 판별용으로 발목의 최상위 조상(대퇴 상단)도 같이 뽑는다.
    for side in ("l", "r"):
        foot_name = found[side + "f"]
        if foot_name is None:
            continue
        bone = armature.data.bones[foot_name]
        while bone.parent is not None and classify_bone(bone.parent.name) is None:
            bone = bone.parent
        found[side + "u"] = bone.name
    return found, warnings


def import_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Blender 5.x의 신형 C++ 임포터를 우선 사용함. 레거시 Python 임포터는
    # 메시 없는 모션 전용 FBX에서 본을 잃는 사례가 있음.
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=path)
    else:
        bpy.ops.import_scene.fbx(
            filepath=path,
            automatic_bone_orientation=False,
            use_anim=True,
        )
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("FBX에서 Armature를 찾지 못함")
    # 본이 가장 많은 아마추어를 주 리그로 사용함.
    armature = max(armatures, key=lambda a: len(a.data.bones))
    # 일부 FBX는 메시↔아마추어 순환 부모로 들어와 depsgraph 사이클이
    # 평가 결과를 오염시킨다. 아마추어의 부모만 끊으면 자체 애니메이션은 유지된다.
    if armature.parent is not None:
        armature.parent = None
    return armature


def frame_range(armature):
    """액션 프레임 범위를 결정한다."""
    action = None
    if armature.animation_data and armature.animation_data.action:
        action = armature.animation_data.action
    elif bpy.context.scene.animation_data and bpy.context.scene.animation_data.action:
        action = bpy.context.scene.animation_data.action
    if action is not None:
        start, end = action.frame_range
        return int(start), int(end)
    scene = bpy.context.scene
    return scene.frame_start, scene.frame_end


def world_head(eval_obj, pose_bone):
    return eval_obj.matrix_world @ pose_bone.head


def world_tail(eval_obj, pose_bone):
    return eval_obj.matrix_world @ pose_bone.tail


def main():
    args = parse_args()
    input_path = os.path.abspath(args["input"])
    output_path = os.path.abspath(args["output"])
    fps = float(args.get("fps", 60.0))

    armature = import_fbx(input_path)
    bones, warnings = pick_bones(armature)
    missing = [key for key in ("lf", "lt", "rf", "rt") if bones[key] is None]
    if missing:
        names = sorted(b.name for b in armature.data.bones)
        raise RuntimeError(
            f"필수 발 본 미발견: {missing}. 사용 가능한 본 목록: {names}")

    scene = bpy.context.scene
    start, end = frame_range(armature)
    depsgraph = bpy.context.evaluated_depsgraph_get()

    samples = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        eval_obj = armature.evaluated_get(depsgraph)
        pose = eval_obj.pose.bones
        samples.append({
            "frame": frame,
            "t": (frame - start) / fps,
            "lf": list(world_head(eval_obj, pose[bones["lf"]])),
            "lt": list(world_head(eval_obj, pose[bones["lt"]])),
            "rf": list(world_head(eval_obj, pose[bones["rf"]])),
            "rt": list(world_head(eval_obj, pose[bones["rt"]])),
            "lu": list(world_head(eval_obj, pose[bones["lu"]])),
            "ru": list(world_head(eval_obj, pose[bones["ru"]])),
        })

    # 상방 축 자동 검출: 대퇴 상단→발목 벡터의 전 구간 평균은 항상 하방을 가리키므로
    # 가장 큰 성분의 축이 상방이다(T자세 팔 벌림에 강함).
    leg_mean = [0.0] * 3
    for sample in samples:
        for axis in range(3):
            leg_mean[axis] += (
                (sample["lf"][axis] - sample["lu"][axis]) +
                (sample["rf"][axis] - sample["ru"][axis])) / 2.0
    up_axis = max(range(3), key=lambda a: abs(leg_mean[a]))
    # 대퇴 상단→발목 평균 길이는 키의 대략 42%로 본다(개략 추정치).
    human_height = abs(leg_mean[up_axis]) / len(samples) / 0.42
    if abs(leg_mean[up_axis]) < abs(sorted(leg_mean, key=abs)[1]) * 1.2:
        warnings.append(
            f"상방 축 판별 마진이 작음: leg_mean={leg_mean}")

    document = {
        "version": 1,
        "source_file": os.path.basename(input_path),
        "coordinate": "blender_local",
        "up_axis": "xyz"[up_axis],
        "fps": fps,
        "frame_start": start,
        "frame_end": end,
        "frame_count": len(samples),
        "human_height": human_height,
        "bones": bones,
        "warnings": warnings,
        "samples": samples,
    }
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as handle:
        json.dump(document, handle, ensure_ascii=False)
    print(f"[foot-dump] {len(samples)}프레임 기록 → {output_path}")
    for warning in warnings:
        print(f"[foot-dump][warn] {warning}")


main()
