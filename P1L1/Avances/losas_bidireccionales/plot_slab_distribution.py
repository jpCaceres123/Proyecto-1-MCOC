"""Plot line-load diagrams from a slab distribution JSON file."""

import json
import sys
from pathlib import Path

import matplotlib.pyplot as plt


def main():
    if len(sys.argv) != 2:
        raise SystemExit("Uso: python plot_slab_distribution.py distribution.json")
    input_path = Path(sys.argv[1])
    data = json.loads(input_path.read_text(encoding="utf-8"))
    colors = {
        "y_min_beam_x": "#1976d2",
        "y_max_beam_x": "#1565c0",
        "x_min_beam_y": "#2e7d32",
        "x_max_beam_y": "#43a047",
    }
    labels = {
        "y_min_beam_x": "Borde y minimo, viga X",
        "y_max_beam_x": "Borde y maximo, viga X",
        "x_min_beam_y": "Borde x minimo, viga Y",
        "x_max_beam_y": "Borde x maximo, viga Y",
    }

    for panel in data["panels"]:
        fig, axes = plt.subplots(2, 2, figsize=(11, 7), squeeze=False)
        for axis, (edge, points) in zip(axes.flat, panel["edges"].items()):
            axis.plot(
                [point["position_m"] for point in points],
                [point["load_kN_m"] for point in points],
                color=colors[edge],
                marker="o",
                linewidth=2.0,
                label=labels[edge],
            )
            axis.set_title(labels[edge])
            axis.set_xlabel("Posicion sobre el borde (m)")
            axis.set_ylabel("Carga lineal (kN/m)")
            axis.grid(True, alpha=0.3)
            axis.legend()
        fig.suptitle(f"Distribucion bidireccional: {panel['panel_id']}")
        fig.tight_layout()
        safe_id = "".join(char if char.isalnum() or char in "-_" else "_"
                           for char in panel["panel_id"])
        output_path = input_path.with_name(f"{input_path.stem}_{safe_id}.png")
        fig.savefig(output_path, dpi=180)
        plt.close(fig)
        print(output_path)


if __name__ == "__main__":
    main()
