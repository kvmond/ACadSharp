# Air CAD office round-trip patch — 2026-10-06

Base: `f4e3bcc3178e40124b8ae6eead85c049afcc8c0e`, branch `codex/aircad-3.8.0`.
Informational version: `3.8.0-aircad.2`; upstream assembly/package version remains 3.8.0.
The Air CAD user authorized publication after reviewing the prepared patch.

## Changes and review

- Write BLOCK_RECORD preview length and bytes in DWG. Write DXF group 310 in chunks of at most 127 bytes.
- Accumulate repeated DXF 310 chunks in the block-record template instead of replacing earlier chunks.
- Read manual dynamic MTEXT column count. Serialize manual dynamic column counts from Heights.Count in DWG/DXF;
  preserve static and automatic-height column counts. Only manual dynamic columns serialize DXF heights.
- Read and write the actual R2000+ DIMATFIT property instead of using the legacy DIMFIT field/constant 3.
  Leave the R14 DIMFIT path unchanged. Modern reads do not overwrite the unrelated legacy DimensionFit property.

The DXF reader template is the only additional storage needed for chunk accumulation; no general serializer
refactoring was needed. Review found and fixed repeated-chunk overwrite and the constant DIMATFIT writer value.
TABLESTYLE defaults, dynamic blocks, view styles, and LEADER behavior are unchanged.

## Verification

`OfficeRoundtripTests`: 12 passing cases using the Air CAD .NET 10/xUnit runner (source linked temporarily).
The original fork net9/net48 test runners were not run in this environment.

- Two generations of DWG AC1015/AC1018/AC1032 and text/binary DXF preview serialization.
- Null/empty previews, lengths 1/126/127/128/254/255/256/513, model/paper/named blocks and geometry preservation.
- Manual dynamic MTEXT with stale count and two heights; static/automatic-height count preservation.
- DIMATFIT 0, 1, 2, 3 across two DWG generations.

App integration also checks edited/unedited verified saves with exact preview bytes and no false loss notice,
all four new/inherited fit settings, first dimension in a document without Standard, strict rejection of other
object/header differences, and existing header/code-page/reader patches. See the app's F2.2 stage record.
Office AutoCAD 2025 reopen/AUDIT and the 13/31 business drawing copies are not available here and remain unverified.
