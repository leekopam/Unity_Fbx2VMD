import math
import struct
import sys


def read_bin(path):
    with open(path, "rb") as f:
        (count,) = struct.unpack("<i", f.read(4))
        verts = []
        for _ in range(count):
            x, y, z = struct.unpack("<fff", f.read(12))
            verts.append((x, y, z))
    return verts


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def norm(a):
    n = math.sqrt(dot(a, a))
    if n < 1e-12:
        return (0.0, 0.0, 0.0)
    return (a[0] / n, a[1] / n, a[2] / n)


def tri_normal(a, b, c):
    return norm(cross(sub(b, a), sub(c, a)))


def dihedral(v, edge_a, edge_b, opp_a, opp_b):
    n1 = tri_normal(v[edge_a], v[edge_b], v[opp_a])
    n2 = tri_normal(v[edge_b], v[edge_a], v[opp_b])
    d = max(-1.0, min(1.0, dot(n1, n2)))
    return math.degrees(math.acos(d)), n1, n2


def centroid(v, ids):
    sx = sum(v[i][0] for i in ids)
    sy = sum(v[i][1] for i in ids)
    sz = sum(v[i][2] for i in ids)
    n = len(ids)
    return (sx / n, sy / n, sz / n)


def main():
    path = sys.argv[1]
    verts = read_bin(path)
    print(f"verts={len(verts)}")
    # pair286: shared edge (4197,4203), opposite verts (4198,4199)
    ang, n1, n2 = dihedral(verts, 4197, 4203, 4198, 4199)
    print(f"pair286 dihedral={ang:.2f} deg")
    ids = (4197, 4203, 4198, 4199)
    c = centroid(verts, ids)
    print(f"pair286 centroid={c[0]:.4f},{c[1]:.4f},{c[2]:.4f}")
    for i in ids:
        p = verts[i]
        print(f"v{i}=({p[0]:.4f},{p[1]:.4f},{p[2]:.4f})")


if __name__ == "__main__":
    main()
