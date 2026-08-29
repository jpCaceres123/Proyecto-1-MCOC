"""Create a simple, scene-independent view of the LT1 benchmark geometry."""

from pathlib import Path

import matplotlib.pyplot as plt

from benchmark_3d import X_COORDS_M, Y_COORDS_M, Z_LEVELS_M


OUT_PATH = Path(__file__).resolve().parent / "results" / "benchmark_geometry.png"


def main():
    fig = plt.figure(figsize=(10, 7))
    ax = fig.add_subplot(111, projection="3d")

    for x in X_COORDS_M:
        for y in Y_COORDS_M:
            ax.plot([x, x], [y, y], [Z_LEVELS_M[0], Z_LEVELS_M[-1]],
                    color="#34495e", linewidth=1.0)

    # The bottom level contains supports only; floor beams start above it.
    for z in Z_LEVELS_M[1:]:
        for y in Y_COORDS_M:
            ax.plot(X_COORDS_M, [y] * len(X_COORDS_M), [z] * len(X_COORDS_M),
                    color="#2878b5", linewidth=0.9)
        for x in X_COORDS_M:
            ax.plot([x] * len(Y_COORDS_M), Y_COORDS_M, [z] * len(Y_COORDS_M),
                    color="#d17a22", linewidth=0.9)

    node_x = [x for z in Z_LEVELS_M for x in X_COORDS_M for y in Y_COORDS_M]
    node_y = [y for z in Z_LEVELS_M for x in X_COORDS_M for y in Y_COORDS_M]
    node_z = [z for z in Z_LEVELS_M for x in X_COORDS_M for y in Y_COORDS_M]
    ax.scatter(node_x, node_y, node_z, color="black", s=18, depthshade=False,
               label="Nodos")

    for i, x in enumerate(X_COORDS_M):
        ax.text(x, Y_COORDS_M[-1] + 0.35, Z_LEVELS_M[0], f"X{i + 1}", color="#2878b5")
    for i, y in enumerate(Y_COORDS_M):
        ax.text(X_COORDS_M[-1] + 0.35, y, Z_LEVELS_M[0], f"Y{i + 1}", color="#d17a22")

    ax.set_xlabel("X (m)")
    ax.set_ylabel("Y (m)")
    ax.set_zlabel("Z (m)")
    ax.set_title("LT1 3D OpenSees benchmark: axes and idealized frame")
    ax.view_init(elev=22, azim=-58)
    ax.legend(loc="upper left")
    fig.tight_layout()
    fig.savefig(OUT_PATH, dpi=180)
    print(OUT_PATH)


if __name__ == "__main__":
    main()
