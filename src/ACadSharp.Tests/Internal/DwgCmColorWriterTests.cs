using ACadSharp.IO.DWG;
using System.IO;
using System.Text;
using Xunit;

namespace ACadSharp.Tests.Internal;

/// <summary>
/// The CMC color the writers of AC1018 and later write (Air CAD W1-1, 3.8.0-aircad.26) holds the method byte AutoCAD writes: ByLayer 0xC0,
/// ByBlock 0xC1, a true color 0xC2 and an index 0xC3. Before, ByBlock was written as the index 0 (0xC3000000). The reader reads both as
/// ByBlock, but AutoCAD keeps the stored value, so a saved copy changed the ByBlock colors of the multileader, dimension and table styles, of the
/// multileaders and of the header's dimension variables (AutoCAD's dump of a copy: MLEADERSTYLE 91 -1056964608 became -1023410176). The object
/// writer and the header writer of AC1021 and later write through the merged writer, which has its own copy of the CMC branch; both are checked.
/// </summary>
public class DwgCmColorWriterTests
{
	public static TheoryData<ACadVersion, bool> Writers { get; } = new()
	{
		{ ACadVersion.AC1018, false },
		{ ACadVersion.AC1018, true },
		{ ACadVersion.AC1021, false },
		{ ACadVersion.AC1021, true },
		{ ACadVersion.AC1024, false },
		{ ACadVersion.AC1024, true },
		{ ACadVersion.AC1027, false },
		{ ACadVersion.AC1027, true },
		{ ACadVersion.AC1032, false },
		{ ACadVersion.AC1032, true },
	};

	[Theory]
	[MemberData(nameof(Writers))]
	public void TheMethodByteIsTheOneAutoCadWrites(ACadVersion version, bool merged)
	{
		(Color Color, uint Rgb)[] cases =
		[
			(Color.ByLayer, 0xC0000000),
			(Color.ByBlock, 0xC1000000),
			(new Color(1), 0xC3000001),
			(new Color(255), 0xC30000FF),
			(new Color(0x12, 0x34, 0x56), 0xC2123456),
		];
		MemoryStream stream = new MemoryStream();
		IDwgStreamWriter writer = merged
			? DwgStreamWriterBase.GetMergedWriter(version, stream, Encoding.ASCII)
			: DwgStreamWriterBase.GetStreamWriter(version, stream, Encoding.ASCII);
		Assert.Equal(merged, writer is DwgMergedStreamWriter);
		foreach ((Color color, uint _) in cases)
		{
			writer.WriteCmColor(color);
		}
		// The CMC goes to the main stream alone; the merged writer's own end also writes its text and handle streams.
		(writer is DwgMergedStreamWriter m ? m.Main : writer).WriteSpearShift();

		IDwgStreamReader reader = DwgStreamReaderBase.GetStreamHandler(version, stream, resetPositon: true);
		foreach ((Color color, uint rgb) in cases)
		{
			long start = reader.PositionInBits();
			Assert.Equal(0, reader.ReadBitShort());
			Assert.Equal(rgb, (uint)reader.ReadBitLong());
			Assert.Equal(0, reader.ReadByte());
			reader.SetPositionInBits(start);
			Assert.Equal(color, reader.ReadCmColor());
		}
	}
}
