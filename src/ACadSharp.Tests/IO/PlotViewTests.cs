using System.IO;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A layout plotted by a named view (plot area View, DXF 74 = 3, view 6): R2004 and later keep the view as a hard pointer,
/// which the DWG reader used to read and drop and the writer to write null, so the layout lost its view on every save.
/// </summary>
public class PlotViewTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void ALayoutKeepsItsPlotViewThroughADwgRoundTrip(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var view = new View("SHEET-AREA") { Center = new CSMath.XY(100, 50), Width = 200, Height = 100 };
		document.Views.Add(view);
		var layout = document.Layouts[Layout.PaperLayoutName];
		layout.PlotType = PlotType.View;
		layout.PlotViewName = "SHEET-AREA";

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		var readLayout = read.Layouts[Layout.PaperLayoutName];
		Assert.Equal(PlotType.View, readLayout.PlotType);
		Assert.Equal("SHEET-AREA", readLayout.PlotViewName);
		Assert.Same(read.Views["SHEET-AREA"], readLayout.PlotView);
	}

	[Fact]
	public void APlotViewSetAsAnObjectIsWrittenByItsName()
	{
		var document = new CadDocument();
		document.Header.Version = ACadVersion.AC1032;
		var view = new View("BY-OBJECT");
		document.Views.Add(view);
		var layout = document.Layouts[Layout.PaperLayoutName];
		layout.PlotType = PlotType.View;
		layout.PlotView = view;
		Assert.Equal("BY-OBJECT", layout.PlotViewName); // the object gives the name

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal("BY-OBJECT", read.Layouts[Layout.PaperLayoutName].PlotViewName);
	}
}
