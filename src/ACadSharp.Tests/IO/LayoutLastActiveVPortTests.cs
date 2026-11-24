using System.IO;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// The model layout's last active viewport (DXF 331) names the <c>*Active</c> VPORT, and it is read and written again (Air CAD W1-2): the reader took
/// only a viewport entity there and the writer wrote null, so a saved copy lost it (AutoCAD's dump of the copy lacked the model layout's 331). A VPORT
/// of another document is written null, so no handle names an object the file lacks.
/// </summary>
public class LayoutLastActiveVPortTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheModelLayoutsActiveVPortSurvivesADwgRoundTrip(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Layouts[Layout.ModelLayoutName].LastActiveVPort = document.VPorts[VPort.DefaultName];

		var read = RoundTrip(document);

		Assert.Equal(VPort.DefaultName, read.Layouts[Layout.ModelLayoutName].LastActiveVPort?.Name);
		Assert.Null(read.Layouts[Layout.ModelLayoutName].LastActiveViewport);
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void AVPortOfAnotherDocumentIsWrittenNull(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var other = new CadDocument();
		document.Layouts[Layout.ModelLayoutName].LastActiveVPort = other.VPorts[VPort.DefaultName];

		var read = RoundTrip(document);

		Assert.Null(read.Layouts[Layout.ModelLayoutName].LastActiveVPort);
	}

	private static CadDocument RoundTrip(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}
}
