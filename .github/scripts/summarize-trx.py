"""Summarize dotnet test TRX files for the GitHub Actions job summary.

Usage: python summarize-trx.py <results-dir> [required-project ...]

Writes a Markdown table (one row per test project) to $GITHUB_STEP_SUMMARY when set, otherwise to stdout.
Exits with code 1 when any test failed, or when a required project is missing or has skipped/not-executed
tests. The SQL Server container tests report "Inconclusive" when Docker is unavailable; requiring that
project stops CI from going green without exercising SQL Server.
"""

import ntpath
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def project_name(root: ET.Element, fallback: str) -> str:
    test = root.find("t:TestDefinitions/t:UnitTest", NS)
    storage = test.get("storage") if test is not None else None
    if not storage:
        return fallback
    # The storage path is the test DLL; normalize Windows and Linux separators and casing.
    return ntpath.basename(storage.replace("/", "\\")).removesuffix(".dll").lower()


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    results_dir = Path(sys.argv[1])
    required = [name.lower() for name in sys.argv[2:]]
    rows = []
    for trx in sorted(results_dir.rglob("*.trx")):
        root = ET.parse(trx).getroot()
        counters = root.find("t:ResultSummary/t:Counters", NS)
        if counters is None:
            continue
        count = lambda key: int(counters.get(key, "0"))
        rows.append({
            "project": project_name(root, trx.stem),
            "total": count("total"),
            "passed": count("passed"),
            "failed": count("failed") + count("error") + count("timeout") + count("aborted"),
            "skipped": count("inconclusive") + count("notExecuted") + count("notRunnable"),
        })

    problems = []
    if not rows:
        problems.append(f"No TRX files found under {results_dir}.")
    for row in rows:
        if row["failed"]:
            problems.append(f"{row['project']}: {row['failed']} failed test(s).")
    for name in required:
        matches = [row for row in rows if row["project"] == name]
        if not matches:
            problems.append(f"{name}: required test project produced no results.")
        elif any(row["skipped"] or row["passed"] == 0 for row in matches):
            problems.append(f"{name}: required tests were skipped or not executed (is Docker available?).")

    lines = ["## Test results", "", "| Project | Total | Passed | Failed | Skipped |", "|---|---:|---:|---:|---:|"]
    for row in rows:
        icon = "❌" if row["failed"] else ("⚠️" if row["skipped"] else "✅")
        lines.append(f"| {icon} `{row['project']}` | {row['total']} | {row['passed']} | {row['failed']} | {row['skipped']} |")
    totals = {key: sum(row[key] for row in rows) for key in ("total", "passed", "failed", "skipped")}
    lines.append(f"| **Total** | **{totals['total']}** | **{totals['passed']}** | **{totals['failed']}** | **{totals['skipped']}** |")
    lines.append("")
    lines.extend(f"- ❌ {problem}" for problem in problems)
    report = "\n".join(lines) + "\n"

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as summary:
            summary.write(report)
    sys.stdout.reconfigure(encoding="utf-8")
    print(report)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
