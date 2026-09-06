"""Build the benchmark 3D frame in SAP2000 using the CSI API.

Run this file from a Windows computer with SAP2000 installed:

    python sap2000_build_model.py

It creates/overwrites results/sap2000_marco_3d.sdb.
Units are kN, m, C. Geometry and loads match model.py.
"""
from pathlib import Path


# SAP2000 API enum values used by the COM interface.
UNITS_KN_M_C = 6
MAT_CONCRETE = 2
LOAD_DEAD = 1
LOAD_FORCE = 1
DIR_GLOBAL_Z = 6


OUT = Path(__file__).resolve().parents[1] / "results"
OUT.mkdir(exist_ok=True)
SAP_FILE = OUT / "sap2000_marco_3d.sdb"


def check(ret, action):
    """Raise a readable error when a SAP2000 API call fails."""
    if ret not in (0, None):
        raise RuntimeError(f"SAP2000 API error {ret} while trying to {action}")


def get_sap_model():
    try:
        import comtypes.client
    except ImportError as exc:
        raise SystemExit(
            "Falta instalar comtypes: python -m pip install comtypes"
        ) from exc

    try:
        sap_object = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
    except (OSError, WindowsError):  # noqa: F821 - Windows-only exception name
        sap_object = comtypes.client.CreateObject("CSI.SAP2000.API.SapObject")
        sap_object.ApplicationStart()

    return sap_object, sap_object.SapModel


def main():
    sap_object, sap = get_sap_model()

    check(sap.InitializeNewModel(UNITS_KN_M_C), "initialize a new model")
    check(sap.File.NewBlank(), "create a blank model")
    check(sap.SetPresentUnits(UNITS_KN_M_C), "set kN-m units")

    # Same structural data used in model.py.
    lx, ly, h = 6.0, 5.0, 3.0
    e_mod, nu = 25_000_000.0, 0.20  # kN/m2
    q_slab = 5.0  # kN/m2
    w_max = q_slab * ly / 2.0  # 12.5 kN/m maximum two-way tributary load

    check(sap.PropMaterial.SetMaterial("CONC_25GPa", MAT_CONCRETE), "define material")
    check(
        sap.PropMaterial.SetMPIsotropic("CONC_25GPa", e_mod, nu, 0.0),
        "set elastic concrete properties",
    )

    # SAP2000 rectangular frame section inputs are depth t3 and width t2.
    check(sap.PropFrame.SetRectangle("COL_30x30", "CONC_25GPa", 0.30, 0.30), "define columns")
    check(sap.PropFrame.SetRectangle("BEAM_25x40", "CONC_25GPa", 0.40, 0.25), "define beams")

    coords = {
        "N1": (0.0, 0.0, 0.0),
        "N2": (lx, 0.0, 0.0),
        "N3": (lx, ly, 0.0),
        "N4": (0.0, ly, 0.0),
        "N5": (0.0, 0.0, h),
        "N6": (lx, 0.0, h),
        "N7": (lx, ly, h),
        "N8": (0.0, ly, h),
    }
    for name, (x, y, z) in coords.items():
        check(sap.PointObj.AddCartesian(x, y, z, name), f"create joint {name}")

    fixed = [True, True, True, True, True, True]
    for name in ("N1", "N2", "N3", "N4"):
        check(sap.PointObj.SetRestraint(name, fixed), f"fix support {name}")

    frames = {
        "C1": ("N1", "N5", "COL_30x30"),
        "C2": ("N2", "N6", "COL_30x30"),
        "C3": ("N3", "N7", "COL_30x30"),
        "C4": ("N4", "N8", "COL_30x30"),
        "BX1": ("N5", "N6", "BEAM_25x40"),
        "BX2": ("N8", "N7", "BEAM_25x40"),
        "BY1": ("N5", "N8", "BEAM_25x40"),
        "BY2": ("N6", "N7", "BEAM_25x40"),
    }
    for name, (i_node, j_node, section) in frames.items():
        check(sap.FrameObj.AddByPoint(i_node, j_node, name, section), f"create frame {name}")

    # Load pattern without automatic self weight. The OpenSees model only includes slab load.
    check(sap.LoadPatterns.Add("LOSA", LOAD_DEAD, 0.0, True), "define slab load pattern")

    for frame_name in ("BX1", "BX2"):
        check(
            sap.FrameObj.SetLoadDistributed(
                frame_name, "LOSA", LOAD_FORCE, DIR_GLOBAL_Z, 0.0, 2.5 / lx, 0.0, -w_max, "Global", True, True
            ),
            f"assign slab load to {frame_name}",
        )
        check(
            sap.FrameObj.SetLoadDistributed(
                frame_name, "LOSA", LOAD_FORCE, DIR_GLOBAL_Z, 2.5 / lx, 3.5 / lx, -w_max, -w_max, "Global", True, False
            ),
            f"assign slab load to {frame_name}",
        )
        check(
            sap.FrameObj.SetLoadDistributed(
                frame_name, "LOSA", LOAD_FORCE, DIR_GLOBAL_Z, 3.5 / lx, 1.0, -w_max, 0.0, "Global", True, False
            ),
            f"assign slab load to {frame_name}",
        )

    for frame_name in ("BY1", "BY2"):
        check(
            sap.FrameObj.SetLoadDistributed(
                frame_name, "LOSA", LOAD_FORCE, DIR_GLOBAL_Z, 0.0, 0.5, 0.0, -w_max, "Global", True, True
            ),
            f"assign slab load to {frame_name}",
        )
        check(
            sap.FrameObj.SetLoadDistributed(
                frame_name, "LOSA", LOAD_FORCE, DIR_GLOBAL_Z, 0.5, 1.0, -w_max, 0.0, "Global", True, False
            ),
            f"assign slab load to {frame_name}",
        )
    check(sap.View.RefreshView(0, False), "refresh the SAP2000 view")
    check(sap.File.Save(str(SAP_FILE)), "save the SAP2000 model")

    print(f"SAP2000 model saved to: {SAP_FILE}")
    print("Expected total vertical load: 150.0 kN")
    print("Run the LOSA static load case in SAP2000 and compare reactions with results/verification.md")

    # Keep SAP2000 open for inspection.
    _ = sap_object


if __name__ == "__main__":
    main()
