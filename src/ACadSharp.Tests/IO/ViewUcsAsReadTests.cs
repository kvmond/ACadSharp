using System.IO;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A named view's UCS references survive a save (Air CAD W1-23): the reader dropped the named UCS (345) and the base UCS (346) and the writer wrote
/// both null, so a view saved with a named UCS came back with an unnamed one. They are written back while they stay in the UCS table.
/// </summary>
public class ViewUcsAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static CadDocument SaveAndRead(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	private static View UcsView(string name, UCS named, UCS baseUcs) => new(name)
	{
		IsUcsAssociated = true,
		UcsOrigin = new XYZ(1, 2, 0),
		UcsXAxis = XYZ.AxisX,
		UcsYAxis = XYZ.AxisY,
		NamedUcsAsRead = named,
		BaseUcsAsRead = baseUcs,
	};

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheUcsReferencesAreWrittenBackAsRead(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var named = new UCS("W1_23_NAMED");
		var baseUcs = new UCS("W1_23_BASE");
		var gone = new UCS("W1_23_GONE");
		document.UCSs.Add(named);
		document.UCSs.Add(baseUcs);
		document.UCSs.Add(gone);
		document.Views.Add(UcsView("W1_23", named, baseUcs));
		document.Views.Add(UcsView("W1_23_LOST", gone, null));
		document.UCSs.Remove(gone.Name);

		var read = SaveAndRead(document);

		Assert.Equal("W1_23_NAMED", read.Views["W1_23"].NamedUcsAsRead?.Name);
		Assert.Equal("W1_23_BASE", read.Views["W1_23"].BaseUcsAsRead?.Name);
		// A UCS no longer in the table is written null, as before.
		Assert.Null(read.Views["W1_23_LOST"].NamedUcsAsRead);
		Assert.Null(read.Views["W1_23_LOST"].BaseUcsAsRead);
	}
}
