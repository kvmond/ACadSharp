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

## S29 multiline patch — 2026-10-07

`3.8.0-aircad.3` adds three focused fixes:

- Preserve pre-R2018 MLINESTYLE element linetypes: write ByLayer/ByBlock reserved indices and ordinary LTYPE_CONTROL indices. Read ordinary indices through the original ordered control handles, independently of table dictionary enumeration.
- Read DXF element colors into their element, keeping the earlier fill color separate.
- Deep-copy MLINE vertex segment lists without clearing or sharing the original line/fill parameters.

`MultilineRoundtripTests` covers all six supported DWG generations (R14, 2000, 2004, 2010, 2013, 2018), text/binary DXF, nonalphabetical custom linetype order, reserved linetypes, distinct element/fill colors, two saves and independent cloned parameters. The previous 12 office regression cases also pass. All 23 cases run from their original source linked into the Air CAD .NET 10/xUnit runner. The fork's net9/net48 runner was not run: the parent uses a different runner and the fork has no restored test assets on this machine.

App verification includes 1,445 passed DWG tests (81 font-dependent skipped, 15 explicit not run), original AutoCAD sample comparisons, mixed MLINE style verified copies, native geometry/display/snaps and DXF conversion. AutoCAD reopen/AUDIT of newly written MLINE copies remains unverified; existing committed oracles are unchanged.

### S29 creation integration follow-up

`3.8.0-aircad.4` fixes three defects exposed by native creation and extended round trips:

- Resolve the DWG CMLSTYLE header handle as MLineStyle, not TableEntry. A custom current style now survives reading and DXF-to-DWG conversion.
- Read/write the complete MLINE flag word, retaining Has, Closed and suppressed start/end caps. The former reader dropped Has on closed entities and the writer dropped cap suppression. This matches the bit-short flags field in the primary LibreDWG `src/dwg.spec` MLINE definition.
- In R13/R14 common entity data, emit the linetype handle only when IsByLayer is false. The reversed condition corrupted following handles and lost entities in multi-object R14 tests.

Review kept these fixes at their serialization boundaries; no round-trip check was weakened. `MultilineRoundtripTests` now covers all five flag combinations, both current-style settings, ByLayer/ByBlock/custom entity linetypes and stable style references across two generations in all previously covered formats. All 11 cases pass through the same linked .NET 10 runner; 18 checks including library version and project reference rules pass. The app-wide DWG run had 1,432 passed, 18 failed (case-only MLINE style name comparisons), 81 font skips and 15 explicit not run. Those 18 were fixed with a narrowly case-insensitive name comparison and a test that a different style still reports loss; the affected 267 tests pass. App UI and MCP creation, approval, rollback, Undo/Redo and two-generation verified saves pass. New AutoCAD reopen/AUDIT remains unverified.
