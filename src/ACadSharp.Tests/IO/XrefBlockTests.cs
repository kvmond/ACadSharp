using System.IO;
using ACadSharp.IO;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A symbol table record's external reference block handle (<see cref="TableEntry.XrefBlock"/>). The DWG reader used to read it into slots nothing
/// used and the writer wrote none (STYLE, UCS and VIEW the table control object), so AutoCAD, opening a library-written copy without the xref file,
/// dropped the xref's dependent layers and linetypes (AirCAD F8 18.3).
/// </summary>
public class XrefBlockTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void DependentEntriesKeepTheirXrefBlockThroughADwgRoundTrip(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var xref = new BlockRecord("XR", "xr.dwg");
		document.BlockRecords.Add(xref);
		document.Layers.Add(Dependent(new Layer("XR|L"), xref));
		document.LineTypes.Add(Dependent(new LineType("XR|DASHED"), xref));
		document.TextStyles.Add(Dependent(new TextStyle("XR|S"), xref));
		document.DimensionStyles.Add(Dependent(new DimensionStyle("XR|D"), xref));
		document.BlockRecords.Add(new BlockRecord("XR|B") { XrefBlock = xref });
		document.Layers.Add(new Layer("OWN"));
		document.Views.Add(new View("V"));
		document.UCSs.Add(new UCS("U"));

		// An entry copied from another document keeps that document's block record: written as none, not as a handle of this document.
		var other = new CadDocument();
		var otherXref = new BlockRecord("OTHER", "other.dwg");
		other.BlockRecords.Add(otherXref);
		document.Layers.Add(new Layer("FOREIGN") { XrefBlock = otherXref });

		// An entry whose xref was removed from the document (detached) points nowhere either.
		var gone = new BlockRecord("GONE", "gone.dwg");
		document.BlockRecords.Add(gone);
		document.Layers.Add(new Layer("GONE|L") { XrefBlock = gone });
		document.BlockRecords.Remove("GONE");

		var read = RoundTrip(document);

		var readXref = read.BlockRecords["XR"];
		Assert.Same(readXref, read.Layers["XR|L"].XrefBlock);
		Assert.Same(readXref, read.LineTypes["XR|DASHED"].XrefBlock);
		Assert.Same(readXref, read.TextStyles["XR|S"].XrefBlock);
		Assert.Same(readXref, read.DimensionStyles["XR|D"].XrefBlock);
		Assert.Same(readXref, read.BlockRecords["XR|B"].XrefBlock);
		Assert.Null(read.Layers["OWN"].XrefBlock);
		Assert.Null(read.Layers["FOREIGN"].XrefBlock);
		Assert.Null(read.Layers["GONE|L"].XrefBlock);
		Assert.Null(read.Views["V"].XrefBlock);
		Assert.Null(readXref.XrefBlock);
		Assert.Null(read.TextStyles["Standard"].XrefBlock);

		// Read, written and read again: the handle stays.
		var again = RoundTrip(read);
		Assert.Same(again.BlockRecords["XR"], again.Layers["XR|L"].XrefBlock);
		Assert.Same(again.BlockRecords["XR"], again.LineTypes["XR|DASHED"].XrefBlock);
	}

	private static T Dependent<T>(T entry, BlockRecord xref) where T : TableEntry
	{
		((TableEntry)entry).Flags |= StandardFlags.XrefDependent | StandardFlags.XrefResolved;
		entry.XrefBlock = xref;
		return entry;
	}

	private static CadDocument RoundTrip(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}
}
