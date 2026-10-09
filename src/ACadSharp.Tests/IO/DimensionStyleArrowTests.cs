using System.IO;
using ACadSharp.Entities;
using ACadSharp.Header;
using ACadSharp.IO;
using ACadSharp.IO.DWG;
using ACadSharp.IO.Templates;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// DIMSTYLE arrow references (DIMBLK 342, DIMLDRBLK 341, DIMBLK1 343, DIMBLK2 344) on synthetic drawings only.
/// The reader used to leave DIMBLK unassigned and give the legacy DIMBLK name to the leader arrow, so every save
/// wrote a null DIMBLK.
/// </summary>
public class DimensionStyleArrowTests
{
	public static TheoryData<ACadVersion, string> Formats => new()
	{
		{ ACadVersion.AC1014, "dwg" },
		{ ACadVersion.AC1018, "dwg" },
		{ ACadVersion.AC1032, "dwg" },
		{ ACadVersion.AC1018, "dxf" },
		{ ACadVersion.AC1032, "dxf" },
		{ ACadVersion.AC1032, "binary" },
	};

	[Theory]
	[MemberData(nameof(Formats))]
	public void ArrowReferencesSurviveTwoGenerations(ACadVersion version, string format)
	{
		var document = Source(version);
		for (int generation = 1; generation <= 2; generation++)
		{
			document = RoundTrip(document, format);
			var style = document.DimensionStyles["ARROWED"];
			Assert.Equal("PROBE_ARROW", style.ArrowBlock?.Name);
			Assert.Same(document.BlockRecords["PROBE_ARROW"], style.ArrowBlock);
			// R13/R14 DWG stores DIMBLK, DIMBLK1 and DIMBLK2 by name only and has no leader arrow.
			Assert.Equal(version == ACadVersion.AC1014 && format == "dwg" ? null : "PROBE_LEADER", style.LeaderArrow?.Name);
			Assert.Equal("PROBE_FIRST", style.DimArrow1?.Name);
			Assert.Equal("PROBE_SECOND", style.DimArrow2?.Name);
			Assert.Null(document.DimensionStyles["PLAIN"].ArrowBlock);
			Assert.Null(document.DimensionStyles["PLAIN"].LeaderArrow);
		}
	}

	[Fact]
	public void HeaderArrowAndLeaderArrowKeepTheirOwnNames()
	{
		var document = new CadDocument(ACadVersion.AC1032);
		var arrow = Block(document, "PROBE_ARROW");
		var leader = Block(document, "PROBE_LEADER");
		var builder = new DwgDocumentBuilder(ACadVersion.AC1032, document, new DwgReaderConfiguration());
		builder.AddTemplate(new CadBlockRecordTemplate(arrow));
		builder.AddTemplate(new CadBlockRecordTemplate(leader));
		var handles = new DwgHeaderHandlesCollection { DIMBLK = arrow.Handle, DIMLDRBLK = leader.Handle };
		var header = new CadHeader(document);

		handles.UpdateHeader(header, builder);

		Assert.Equal("PROBE_ARROW", header.DimensionBlockName);
		Assert.Equal("PROBE_LEADER", header.ArrowBlockName);
	}

	private static CadDocument Source(ACadVersion version)
	{
		var document = new CadDocument(version);
		document.Header.PlotStyleMode = 1;
		document.DimensionStyles.Add(new DimensionStyle("ARROWED")
		{
			ArrowBlock = Block(document, "PROBE_ARROW"),
			LeaderArrow = Block(document, "PROBE_LEADER"),
			SeparateArrowBlocks = true,
			DimArrow1 = Block(document, "PROBE_FIRST"),
			DimArrow2 = Block(document, "PROBE_SECOND"),
		});
		document.DimensionStyles.Add(new DimensionStyle("PLAIN"));
		return document;
	}

	private static BlockRecord Block(CadDocument document, string name)
	{
		var block = new BlockRecord(name);
		block.Entities.Add(new Line(new XYZ(-1, -.5, 0), XYZ.Zero));
		document.BlockRecords.Add(block);
		return block;
	}

	private static CadDocument RoundTrip(CadDocument document, string format)
	{
		using var stream = new MemoryStream();
		if (format == "dwg")
		{
			DwgWriter.Write(stream, document);
			return DwgReader.Read(new MemoryStream(stream.ToArray()));
		}
		DxfWriter.Write(stream, document, binary: format == "binary");
		return DxfReader.Read(new MemoryStream(stream.ToArray()));
	}
}
