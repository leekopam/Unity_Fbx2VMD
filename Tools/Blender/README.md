# Blender 연계 개발 환경

상주 headless Blender + 소켓 브리지. 테스트·픽스처 생성·검증 등 **개발 환경 전용** — 프로덕션 파이프라인(FBX→VMD 변환 절차)에는 들어가지 않는다.

## 구성

- `bpy.py` — 클라이언트 CLI. Blender 미기동 시 자동으로 띄운다.
- `blender_bridge.py` — Blender 내부에서 도는 소켓 서버(127.0.0.1:8765). 코드를 exec하고 stdout/에러를 JSON으로 돌려준다. exec 전역은 세션 간 유지되어 변수가 살아있다.
- `../BlenderFootDump/dump_foot_trajectory.py` — 발 궤적 JSON 덤프(기존, 단독/브리지 양쪽 호환).

## 사용

```powershell
py Tools/Blender/bpy.py ensure                                        # 브리지 기동(멱등)
py Tools/Blender/bpy.py exec "import bpy; print(bpy.app.version)"     # 임의 코드 실행
py Tools/Blender/bpy.py run 스크립트.py -- --input a.fbx              # 파일 실행(+ argv)
py Tools/Blender/bpy.py shutdown                                      # 종료
```

`run`은 파일 내용을 Blender 내부에서 exec한다. `--` 뒤 인자는 스크립트에 `sys.argv`로 전달된다.

## Blender 경로

`FBX2VMD_BLENDER` 환경변수 → `C:\Program Files\Blender Foundation\Blender*\blender.exe` 최신 버전 → PATH 순으로 탐색한다.

## 알려진 주의점

- Blender 5.0의 레거시 `bpy.ops.import_scene.fbx`는 이 프로젝트 FBX에서 오브젝트를 못 읽는다 — 신형 `bpy.ops.wm.fbx_import` 사용(dump 스크립트 참고).
- 일부 FBX는 메시↔아마추어 순환 부모로 임포트되어 depsgraph 사이클로 월드 행렬이 오염된다 — dump 스크립트의 부모 절단 처리를 참고.
- 브리지는 로컬 127.0.0.1 전용, 인증 없음 — 이 머신의 개발용 프로세스로만 사용.
