#!/usr/bin/env python3
"""
Export ARLUT MATLAB (*.mat under .../dataset) rows to CSV for SmartEnergyExpert field import:

  POST /api/datasets/{id}/samples/import-csv
  Columns: timestamp,frequencyBand,amplitudeDb,depthMeters,rangeMeters,soundSpeed,noiseLevelDb

Each .mat carries dStruct.metadata (frequency, range, time) plus matrix A (n_samples x hydrophones).

Amplitude uses RMS across hydrophone columns converted to an illustrative dB (20·log₁₀(rms+ε)).

Prerequisites (virtualenv recommended):

  python3 -m venv .venv-arlut
  . .venv-arlut/bin/activate
  pip install -r scripts/requirements-arlut-export.txt

Example:

  ./scripts/arlut_mat_to_field_csv.py \\
    --input-dir /path/to/ARLUT_01/dataset \\
    --output-csv data/ARLUT_01_partA_01_dataset_field_stride2500.csv \\
    --stride 2500
"""

from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path

import numpy as np
from scipy.io import loadmat

MATLAB_DN_UNIX_EPOCH_DAY = 719529  # MATLAB datenum(1970,1,1)


def matlab_dn_to_datetime_utc(dn: float) -> datetime:
    epoch = datetime(1970, 1, 1, tzinfo=timezone.utc)
    return epoch + timedelta(days=float(dn) - MATLAB_DN_UNIX_EPOCH_DAY)


def rms_amplitude_db(a_row: np.ndarray, eps: float = 1e-12) -> float:
    rms = float(np.sqrt(np.mean(np.square(a_row.astype(np.float64))) + eps))
    return float(20.0 * np.log10(max(rms, eps)))


def format_timestamp_utc_iso_z(ts: datetime) -> str:
    u = ts.astimezone(timezone.utc)
    return u.strftime("%Y-%m-%dT%H:%M:%S.%f") + "Z"


@dataclass
class MatSnapshot:
    path: Path
    start_dn: float
    block: int
    f_center_hz: float
    range_m: float
    t_length_s: float
    a: np.ndarray  # shape (n, channels)


def _mat_struct_or_cell(obj):
    """MATLAB struct fields are often stored as (1,1) object arrays of mat_struct."""
    if isinstance(obj, np.ndarray) and obj.dtype == object and obj.size >= 1:
        return obj.flat[0]
    return obj


def read_mat_snapshot(path: Path) -> MatSnapshot:
    raw = loadmat(path.as_posix(), squeeze_me=False, struct_as_record=False)
    ds = raw["dStruct"][0, 0]
    a = np.asarray(raw["A"], dtype=np.float32)
    content = _mat_struct_or_cell(getattr(ds, "content"))
    f_center_raw = getattr(content, "fCenter")
    fc = (
        float(f_center_raw.squeeze().item())
        if isinstance(f_center_raw, np.ndarray)
        else float(f_center_raw)
    )
    dn = getattr(ds, "time")
    dn = float(dn.squeeze().item()) if isinstance(dn, np.ndarray) else float(dn)
    block = getattr(ds, "block")
    block = int(block.squeeze().item()) if isinstance(block, np.ndarray) else int(block)
    rn = getattr(ds, "range")
    rn = float(rn.squeeze().item()) if isinstance(rn, np.ndarray) else float(rn)
    tlen = getattr(ds, "tLength")
    tlen = float(tlen.squeeze().item()) if isinstance(tlen, np.ndarray) else float(tlen)
    return MatSnapshot(path=path, start_dn=dn, block=block, f_center_hz=fc, range_m=rn, t_length_s=tlen, a=a)


def main() -> int:
    p = argparse.ArgumentParser(description="ARLUT .mat dataset → SmartEnergyExpert field CSV.")
    p.add_argument(
        "--input-dir",
        type=Path,
        required=True,
        help="Folder that contains ARLUT01_*.mat (e.g. .../ARLUT_01/dataset).",
    )
    p.add_argument("--output-csv", type=Path, required=True, help="Writable CSV destination path.")
    p.add_argument(
        "--stride",
        type=int,
        default=2500,
        help="Emit every nth sample inside each MAT (reduces CSV size dramatically). Default: 2500.",
    )
    p.add_argument(
        "--depth-m",
        type=float,
        default=37.0,
        help="Recorded depth metres (CSV field; MET does not expose bathymetry consistently). Default: 37.",
    )
    p.add_argument("--sound-speed-mps", type=float, default=1485.0, help="Default: 1485.")
    p.add_argument("--noise-db", type=float, default=-90.0, help="Noise floor placeholder (dB re 1 µPa scale).")

    ns = p.parse_args()
    if ns.stride < 1:
        p.error("--stride must be >= 1")

    paths = sorted(ns.input_dir.glob("ARLUT*.mat"))
    if not paths:
        print(f"No ARLUT*.mat under {ns.input_dir}", file=sys.stderr)
        return 1

    snapshots: list[MatSnapshot] = []
    for path in paths:
        try:
            snapshots.append(read_mat_snapshot(path))
        except Exception as ex:  # noqa: BLE001
            print(f"Skip {path.name}: {ex}", file=sys.stderr)

    snapshots.sort(key=lambda x: (x.start_dn, x.block, x.path.name))

    tuples: list[tuple[datetime, str]] = []
    for snap in snapshots:
        n_rows = snap.a.shape[0]
        if n_rows == 0 or snap.t_length_s <= 0:
            continue
        base_dt = matlab_dn_to_datetime_utc(snap.start_dn)
        for i in range(0, n_rows, ns.stride):
            ts = base_dt + timedelta(seconds=float(i) / float(n_rows) * snap.t_length_s)
            amp_db = rms_amplitude_db(snap.a[i])
            line = ",".join(
                [
                    format_timestamp_utc_iso_z(ts),
                    format(snap.f_center_hz, ".3f"),
                    format(amp_db, ".6f"),
                    format(ns.depth_m, ".3f"),
                    format(snap.range_m, ".6f"),
                    format(ns.sound_speed_mps, ".3f"),
                    format(ns.noise_db, ".3f"),
                ]
            )
            tuples.append((ts, line))

    tuples.sort(key=lambda kv: kv[0])
    ns.output_csv.parent.mkdir(parents=True, exist_ok=True)
    hdr = "timestamp,frequencyBand,amplitudeDb,depthMeters,rangeMeters,soundSpeed,noiseLevelDb\n"
    with ns.output_csv.open("w", encoding="utf-8", newline="\n") as out:
        out.write(hdr)
        for _, ln in tuples:
            out.write(ln)
            out.write("\n")

    print(f"Wrote {len(tuples)} rows to {ns.output_csv} from {len(snapshots)} MAT files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
