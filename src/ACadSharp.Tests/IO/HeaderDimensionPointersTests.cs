using System.IO;
using ACadSharp.IO;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// The header's dimension arrows and line types survive a save (Air CAD DS3): the writer wrote DIMLDRBLK, DIMBLK, DIMBLK1 and DIMBLK2 (R2000 and
/// later) and DIMLTYPE, DIMLTEX1 and DIMLTEX2 (R2007 and later) null, so a drawing whose header named an arrow block came back with the default
/// arrow. The arrows are written by the block names the reader gives the header; the line types as read, while they stay in the line type table.
/// An empty name stays a null pointer.
/// </summary>
public class HeaderDimensionPointersTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static CadDocument SaveAndRead(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheArrowBlocksAreWrittenByName(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.BlockRecords.Add(new BlockRecord("_ArchTick"));
		document.BlockRecords.Add(new BlockRecord("_Dot"));
		document.Header.DimensionBlockName = "_ArchTick";
		document.Header.ArrowBlockName = "_Dot";
		document.Header.DimensionBlockNameFirst = "_ArchTick";
		document.Header.DimensionBlockNameSecond = "NO_SUCH_BLOCK";

		var read = SaveAndRead(document);

		Assert.Equal("_ArchTick", read.Header.DimensionBlockName);
		Assert.Equal("_Dot", read.Header.ArrowBlockName);
		Assert.Equal("_ArchTick", read.Header.DimensionBlockNameFirst);
		// A name no block has is written null, as an empty one: the default arrow.
		Assert.True(string.IsNullOrEmpty(read.Header.DimensionBlockNameSecond));
	}

	[Fact]
	public void TheDefaultArrowStaysNull()
	{
		var document = new CadDocument();
		document.Header.Version = ACadVersion.AC1032;

		var read = SaveAndRead(document);

		Assert.True(string.IsNullOrEmpty(read.Header.DimensionBlockName));
		Assert.True(string.IsNullOrEmpty(read.Header.ArrowBlockName));
		Assert.Null(read.Header.DimensionLineTypeAsRead);
	}

	[Theory]
	[InlineData(ACadVersion.AC1024)]
	[InlineData(ACadVersion.AC1027)]
	[InlineData(ACadVersion.AC1032)]
	public void TheLineTypesAreWrittenBackAsRead(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Header.DimensionLineTypeAsRead = document.LineTypes[LineType.ContinuousName];
		document.Header.DimensionTex1AsRead = document.LineTypes[LineType.ByBlockName];
		var gone = new LineType("W1_DS3_GONE");
		document.LineTypes.Add(gone);
		document.Header.DimensionTex2AsRead = gone;
		document.LineTypes.Remove(gone.Name);

		var read = SaveAndRead(document);

		Assert.Equal(LineType.ContinuousName, read.Header.DimensionLineTypeAsRead?.Name, ignoreCase: true);
		Assert.Equal(LineType.ByBlockName, read.Header.DimensionTex1AsRead?.Name, ignoreCase: true);
		// A line type no longer in the table is written null.
		Assert.Null(read.Header.DimensionTex2AsRead);
	}
}
