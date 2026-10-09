# AutoCAD integration probe

Real DatabaseServices/PlotEngine checks in a disposable Core Console drawing. The probe DLL is not distributed in GKIN-APPLOAD.zip and adds no production command.

Build:

```powershell
dotnet build src/GKIN.csproj -c Release
dotnet build tests/Integration/GkinWorkflowProbe.csproj -c Release
```

Create a temporary `.scr` with absolute paths for the checkout:

```text
_.NETLOAD
D:/path/to/GKIN-NET/tmp/workflow-probe/GKIN.dll
_.NETLOAD
D:/path/to/GKIN-NET/tmp/workflow-probe/GkinWorkflowProbe.dll
GKINWORKFLOWPROBE
_.QUIT
_Y
```

Run in a new scratch drawing, not an open production drawing:

```powershell
$env:GKIN_PROBE_REPORT = 'D:\path\to\GKIN-NET\tmp\probe.txt'
$env:GKIN_PROBE_FIXTURE = '0'
$env:GKIN_PROBE_PDF_DIR = 'D:\path\to\GKIN-NET\tmp\pdfs'
& $env:ACAD_CORE /s 'D:\path\to\GKIN-NET\tmp\probe.scr' /l en-US
Get-Content $env:GKIN_PROBE_REPORT
```

Set ACAD_CORE to the installed accoreconsole.exe. Require `FAILURES=0`; the process exit code alone is not a pass. Fixture entities are discarded on quit. PDF checks require DWG To PDF.pc3. Review rendered pages as well as page counts.

For read-only detection of a real DWG, copy it to a temporary path, set `GKIN_PROBE_FIXTURE=1` and pass `/i` with the copied path. Do not save it. Optional `GKIN_PROBE_FIXTURE_OUTPUT` enables a five-sheet PDF on the copy (four TĐ sheets and four TN windows on one sheet for the reviewed 350 m fixture). These counts are fixture-specific, not expected counts for arbitrary drawings.

Clear GKIN_PROBE_FIXTURE_OUTPUT before running synthetic checks again.
