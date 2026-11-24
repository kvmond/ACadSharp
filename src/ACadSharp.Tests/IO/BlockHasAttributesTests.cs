using System.IO;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A layout's block keeps the "has attribute definitions" bit it was read with (Air CAD W1-9): AutoCAD leaves the bit 0 on *Model_Space although
/// it holds attribute definitions (MS-ROUNDTRIP and F24: three ATTDEFs in model space, BLOCK 70 = 0), and the writer computed the bit from the
/// entities, so a saved copy's model space showed 70 = 2. Any other block still computes it, so a block whose attribute definitions change is
/// written as it is now. A layout not read from a DWG takes the DXF flag (0 for a new one).
/// </summary>
public class BlockHasAttributesTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1032 };

	private static CadDocument RoundTrip(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void ALayoutsBlockKeepsTheBitItWasReadWithAndAnyOtherBlockComputesIt(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Entities.Add(new AttributeDefinition { Tag = "AIR_MS", InsertPoint = XYZ.Zero });
		var withAttributes = new BlockRecord("AIR_ATT");
		withAttributes.Entities.Add(new AttributeDefinition { Tag = "AIR_B", InsertPoint = XYZ.Zero });
		document.BlockRecords.Add(withAttributes);
		var plain = new BlockRecord("AIR_PLAIN");
		plain.Entities.Add(new Line(XYZ.Zero, XYZ.AxisX));
		document.BlockRecords.Add(plain);

		var first = RoundTrip(document);

		Assert.False(first.ModelSpace.HasAttributesAsRead);
		Assert.True(first.BlockRecords["AIR_ATT"].HasAttributesAsRead);
		Assert.False(first.BlockRecords["AIR_PLAIN"].HasAttributesAsRead);

		// A model space read with the bit set keeps it; a block that gains an attribute definition is written with the bit.
		first.ModelSpace.HasAttributesAsRead = true;
		first.BlockRecords["AIR_PLAIN"].Entities.Add(new AttributeDefinition { Tag = "AIR_NEW", InsertPoint = XYZ.Zero });
		var second = RoundTrip(first);

		Assert.True(second.ModelSpace.HasAttributesAsRead);
		Assert.True(second.BlockRecords["AIR_PLAIN"].HasAttributesAsRead);
	}
}
