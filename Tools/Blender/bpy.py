# -*- coding: utf-8 -*-
"""Blender 브리지 클라이언트 — 상주 headless Blender에 python 코드를 보내 실행한다.

사용:
  py Tools/Blender/bpy.py ensure                  # 브리지 기동(이미 떠 있으면 즉시 반환)
  py Tools/Blender/bpy.py exec "print(bpy.app.version_string)"
  py Tools/Blender/bpy.py run Tools/BlenderFootDump/dump_foot_trajectory.py
  py Tools/Blender/bpy.py shutdown                # Blender 종료

환경변수 FBX2VMD_BLENDER 로 blender.exe 경로를 고정할 수 있다.
"""
import glob
import json
import os
import shutil
import socket
import subprocess
import sys
import time

PORT = 8765
HERE = os.path.dirname(os.path.abspath(__file__))
BRIDGE = os.path.join(HERE, "blender_bridge.py")


def find_blender():
    """FBX2VMD_BLENDER → Blender Foundation 설치 폴더(최신) → PATH 순."""
    env = os.environ.get("FBX2VMD_BLENDER") or os.environ.get("BLENDER_EXE")
    if env and os.path.isfile(env):
        return env
    candidates = glob.glob(
        r"C:\Program Files\Blender Foundation\Blender*\blender.exe")
    if candidates:
        return sorted(candidates)[-1]
    return shutil.which("blender")


def send(payload, timeout=600):
    conn = socket.create_connection(("127.0.0.1", PORT), timeout=timeout)
    try:
        conn.sendall(json.dumps(payload).encode("utf-8") + b"\n")
        buf = b""
        while True:
            chunk = conn.recv(1 << 16)
            if not chunk:
                break
            buf += chunk
        return json.loads(buf.decode("utf-8"))
    finally:
        conn.close()


def alive():
    try:
        send({"code": ""}, timeout=2)
        return True
    except OSError:
        return False


def ensure():
    if alive():
        return True
    blender = find_blender()
    if not blender:
        print("blender.exe를 찾지 못했습니다. FBX2VMD_BLENDER로 경로를 지정하세요.",
              file=sys.stderr)
        return False
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    subprocess.Popen(
        [blender, "-b", "--factory-startup", "--python", BRIDGE,
         "--", "--port", str(PORT)],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        creationflags=flags)
    for _ in range(60):
        if alive():
            return True
        time.sleep(1)
    print("블렌더 브리지 기동 시간 초과", file=sys.stderr)
    return False


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    cmd = sys.argv[1]

    if cmd == "ensure":
        return 0 if ensure() else 1
    if cmd == "shutdown":
        if not alive():
            print("브리지가 실행 중이 아닙니다.")
            return 0
        res = send({"cmd": "shutdown"}, timeout=10)
        return 0 if res.get("ok") else 1

    if not ensure():
        return 1
    if cmd == "exec":
        res = send({"code": sys.argv[2] if len(sys.argv) > 2 else ""})
    elif cmd == "run":
        # 파일 뒤 인자는 스크립트의 sys.argv[1:]로 전달된다.
        args = sys.argv[3:]
        if args[:1] == ["--"]:
            args = args[1:]
        res = send({"file": sys.argv[2], "argv": args})
    else:
        print(__doc__)
        return 2

    if res.get("stdout"):
        print(res["stdout"], end="")
    if res.get("error"):
        print(res["error"], file=sys.stderr)
    return 0 if res.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
