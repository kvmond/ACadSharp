using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A dimension's "block used by this dimension only" flag (DXF 70 bit 32, bit 1 of the DWG's flag byte) survives a DWG round trip (Air CAD W1-13):
/// the writer wrote only bit 0, so every dimension AutoCAD read back from a saved copy lacked the 32 (F24: 70 = 102 came back 70), and the reader left
/// the constructor's 32 on whatever the file held. A dimension made without it keeps it off.
/// </summary>
public class DimensionBlockFlagTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheBlockReferenceFlagSurvivesADwgRoundTrip(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		for (var i = 0; i < 2; i++)
		{
			var dimension = new DimensionAligned(new XYZ(0, i * 10, 0), new XYZ(10, i * 10, 0));
			var picture = new BlockRecord("*D" + (i + 1));
			picture.Entities.Add(new Line(new XYZ(0, i * 10, 0), new XYZ(10, i * 10, 0)));
			dimension.Block = picture;
			if (i == 1)
			{
				dimension.Flags &= ~DimensionType.BlockReference;
			}
			document.Entities.Add(dimension);
		}

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		var flags = read.Entities.OfType<DimensionAligned>().OrderBy(d => d.FirstPoint.Y).Select(d => d.Flags.HasFlag(DimensionType.BlockReference)).ToArray();
		Assert.Equal(new[] { true, false }, flags);
	}

	/// <summary>
	/// The flag byte's bits above the two lowest survive as read (AutoCAD sets bit 3, which no DXF group shows: the samples' dimensions stored 0xB
	/// and their saved copies 0x3); a dimension not read from a DWG writes none of them.
	/// </summary>
	[Theory]
	[MemberData(nameof(Versions))]
	public void TheFlagBytesOtherBitsSurviveADwgRoundTrip(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		for (var i = 0; i < 2; i++)
		{
			var dimension = new DimensionAligned(new XYZ(0, i * 10, 0), new XYZ(10, i * 10, 0));
			var picture = new BlockRecord("*D" + (i + 1));
			picture.Entities.Add(new Line(new XYZ(0, i * 10, 0), new XYZ(10, i * 10, 0)));
			dimension.Block = picture;
			dimension.OtherFlagBitsAsRead = i == 0 ? (byte)0b1000 : (byte)0;
			document.Entities.Add(dimension);
		}

		var read = Read(Write(document));
		Assert.Equal(new byte[] { 0b1000, 0 }, Bits(read));
		// And once more, from what was read.
		Assert.Equal(new byte[] { 0b1000, 0 }, Bits(Read(Write(read))));

		static byte[] Bits(CadDocument d) => d.Entities.OfType<DimensionAligned>().OrderBy(x => x.FirstPoint.Y).Select(x => x.OtherFlagBitsAsRead).ToArray();
	}

	private static byte[] Write(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return stream.ToArray();
	}

	private static CadDocument Read(byte[] bytes) => DwgReader.Read(new MemoryStream(bytes));
}
