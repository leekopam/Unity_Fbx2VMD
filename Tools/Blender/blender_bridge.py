# Blender 내부에서 실행되는 소켓 브리지.
# bpy.py 클라이언트가 JSON 라인으로 보낸 python 코드를 exec하고
# stdout/에러를 JSON으로 돌려준다 — 매 호출마다 Blender를 재기동하지 않게 한다.
#
# 사용(직접 실행하지 않고 bpy.py ensure가 자동으로 띄움):
#   blender -b --factory-startup --python blender_bridge.py -- --port 8765
import contextlib
import io
import json
import socket
import sys
import traceback

import bpy  # noqa: F401  exec 컨텍스트에 bpy를 노출하기 위한 임포트


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    port = 8765
    for i, arg in enumerate(argv):
        if arg == "--port" and i + 1 < len(argv):
            port = int(argv[i + 1])

    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind(("127.0.0.1", port))
    server.listen(1)
    print(f"[blender_bridge] listening on 127.0.0.1:{port}", flush=True)

    # 세션 전체에서 공유되는 exec 전역 — obj 변수 등을 호출 간에 유지한다.
    global_ns = {"__name__": "__bridge__", "bpy": bpy}

    while True:
        conn, _ = server.accept()
        try:
            buf = b""
            while not buf.endswith(b"\n"):
                chunk = conn.recv(1 << 16)
                if not chunk:
                    break
                buf += chunk
            request = json.loads(buf.decode("utf-8"))

            if request.get("cmd") == "shutdown":
                conn.sendall(b'{"ok": true, "stdout": "", "error": ""}\n')
                break

            code = request.get("code") or ""
            file_path = request.get("file")
            if file_path:
                with open(file_path, "r", encoding="utf-8") as f:
                    code = f.read()

            # run에 넘어온 인자는 sys.argv로 노출 — "--" 뒤 인자를 쓰는 기존
            # 스크립트(dump_foot_trajectory.py 등)를 그대로 돌릴 수 있다.
            old_argv = sys.argv
            if request.get("argv") is not None:
                sys.argv = [file_path or "<bridge>"] + request["argv"]

            captured = io.StringIO()
            ok, error = True, ""
            with contextlib.redirect_stdout(captured), \
                    contextlib.redirect_stderr(captured):
                try:
                    exec(compile(code, file_path or "<bridge>", "exec"), global_ns)
                except SystemExit:
                    pass  # 스크립트의 sys.exit()가 브리지까지 내리지 않게 흡수
                except Exception:
                    ok = False
                    error = traceback.format_exc()
                finally:
                    sys.argv = old_argv

            conn.sendall(json.dumps(
                {"ok": ok, "stdout": captured.getvalue(), "error": error}
            ).encode("utf-8") + b"\n")
        except Exception:
            traceback.print_exc()
        finally:
            conn.close()

    server.close()


main()
