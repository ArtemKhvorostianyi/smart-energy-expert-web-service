#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
from typing import Any

import numpy as np
import scipy.io as sio


def scalar(value: Any, default: float = 0.0) -> float:
    if value is None:
        return default
    if isinstance(value, (int, float, np.integer, np.floating)):
        return float(value)
    arr = np.asarray(value)
    if arr.size == 0:
        return default
    return float(arr.reshape(-1)[0])


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Convert ARLUT groundtruth MAT files to CSV for the hydroacoustic service.")
    parser.add_argument("--groundtruth-dir", required=True, help="Path to ARLUT groundtruth folder.")
    parser.add_argument("--field-out", required=True, help="Output CSV path for field dataset.")
    parser.add_argument("--simulation-out", required=True, help="Output CSV path for synthetic simulation dataset.")
    parser.add_argument("--sample-step", type=int, default=1000, help="Take every N-th sample from each MAT waveform.")
    return parser.parse_args()


def build_frequency_map(annotations_path: Path) -> dict[int, float]:
    data = sio.loadmat(annotations_path)
    contents = data["contents"]
    freq_map: dict[int, float] = {}
    for i in range(contents.shape[1]):
        cell = contents[0, i]
        try:
            freq_map[i + 1] = scalar(cell["fCenter"][0, 0], default=1000.0)
        except Exception:
            freq_map[i + 1] = 1000.0
    return freq_map


def main() -> None:
    args = parse_args()
    groundtruth_dir = Path(args.groundtruth_dir)
    field_out = Path(args.field_out)
    simulation_out = Path(args.simulation_out)
    step = max(1, args.sample_step)

    annotations_path = groundtruth_dir / "annotations.mat"
    if not annotations_path.exists():
        raise FileNotFoundError(f"annotations.mat not found in {groundtruth_dir}")

    freq_map = build_frequency_map(annotations_path)
    mat_files = sorted(groundtruth_dir.glob("ARLUT*.mat"))
    if not mat_files:
        raise FileNotFoundError("No ARLUT*.mat files found.")

    field_rows: list[str] = ["timestamp,frequencyBand,amplitudeDb,depthMeters,rangeMeters,soundSpeed,noiseLevelDb"]
    simulation_rows: list[str] = ["timestamp,frequencyBand,amplitudeDb,depthMeters,rangeMeters,soundSpeed,noiseLevelDb"]

    base_time = np.datetime64("2026-04-30T00:00:00")
    global_index = 0

    for mat_path in mat_files:
        mat = sio.loadmat(mat_path)
        waveform = np.asarray(mat["A"]).reshape(-1)
        dstruct = mat["dStruct"][0, 0]
        content_id = int(round(scalar(dstruct["contentID"], default=1)))
        frequency = freq_map.get(content_id, 1000.0)
        range_meters = scalar(dstruct["range"], default=1000.0)
        power = scalar(dstruct["power"], default=0.0)

        for i in range(0, waveform.size, step):
            sample = float(waveform[i])
            amplitude_db = 20.0 * np.log10(abs(sample) + 1e-9)
            timestamp = base_time + np.timedelta64(global_index, "ms")
            timestamp_str = str(timestamp) + "Z"

            # Field row from measured signal.
            field_rows.append(
                f"{timestamp_str},{frequency:.3f},{amplitude_db:.6f},37.0,{range_meters:.3f},1485.0,{-90.0 + power:.3f}"
            )

            # Synthetic simulation row: slightly biased/smoothed variant for model-vs-field.
            simulation_amplitude = amplitude_db * 0.97 + 0.6
            simulation_rows.append(
                f"{timestamp_str},{frequency:.3f},{simulation_amplitude:.6f},37.0,{range_meters:.3f},1487.0,{-91.0 + power:.3f}"
            )
            global_index += 1

    field_out.parent.mkdir(parents=True, exist_ok=True)
    simulation_out.parent.mkdir(parents=True, exist_ok=True)
    field_out.write_text("\n".join(field_rows), encoding="utf-8")
    simulation_out.write_text("\n".join(simulation_rows), encoding="utf-8")

    print(f"Generated field CSV: {field_out} ({len(field_rows) - 1} rows)")
    print(f"Generated simulation CSV: {simulation_out} ({len(simulation_rows) - 1} rows)")


if __name__ == "__main__":
    main()
