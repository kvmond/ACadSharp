using System.IO;
using System.Linq;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// The items of an XRECORD in a DWG file (Air CAD W1-19): 38 and 39 are single reals, 110 to 112 and 210 points of three reals, where the DXF table
/// the reader and writer used has points and single reals; read so, every item after the first of them was read from the wrong place and a save
/// wrote what was misread (P0B2-XRECORD-ITEMS of AC1018, AC1024 and AC1027: AutoCAD's dump lists 571 items, the copy's data was 68 bytes short and
/// unreadable). Before R2007 the code -9999 that stands for the 64-bit items, which are in the extended data, is written back where it was.
/// </summary>
public class XRecordDwgItemTypesTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static CadDocument SaveAndRead(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void PointsAndRealsOfTheDwgFormKeepTheirPlaces(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var record = new XRecord("W1_19");
		record.CreateEntry(1, "S1");
		record.CreateEntry(38, 38.5);
		record.CreateEntry(39, 39.5);
		record.CreateEntry(70, (short)70);
		record.CreateEntry(110, new XYZ(110.1, 110.2, 110.3));
		record.CreateEntry(111, new XYZ(111.1, 111.2, 111.3));
		record.CreateEntry(112, new XYZ(112.1, 112.2, 112.3));
		record.CreateEntry(140, 140.5);
		record.CreateEntry(210, new XYZ(210.1, 210.2, 210.3));
		record.CreateEntry(300, "S300");
		document.RootDictionary.Add(record);

		var read = SaveAndRead(document).RootDictionary.GetEntry<XRecord>("W1_19");

		Assert.Equal(record.Entries.Select(e => (e.Code, e.Value)), read.Entries.Select(e => (e.Code, e.Value)));
	}

	[Theory]
	[InlineData(ACadVersion.AC1018, 1)]
	[InlineData(ACadVersion.AC1024, 0)]
	[InlineData(ACadVersion.AC1032, 0)]
	public void TheCodeOfTheSixtyFourBitItemsIsWrittenBackBeforeR2007(ACadVersion version, int expected)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var record = new XRecord("W1_19");
		record.CreateEntry(1, "before");
		record.Int64Placeholders.Add(1);
		record.CreateEntry(2, "after");
		document.RootDictionary.Add(record);

		var read = SaveAndRead(document).RootDictionary.GetEntry<XRecord>("W1_19");

		Assert.Equal(new[] { (1, (object)"before"), (2, "after") }, read.Entries.Select(e => (e.Code, e.Value)));
		Assert.Equal(Enumerable.Repeat(1, expected), read.Int64Placeholders);
	}
}
