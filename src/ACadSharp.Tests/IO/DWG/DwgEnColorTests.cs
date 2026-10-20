using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using System.IO;
using System.Linq;
using Xunit;

namespace ACadSharp.Tests.IO.DWG;

/// <summary>
/// The entity color of R2004 and later DWG files: a color number with flags, then the transparency when 0x2000 is set.
/// AutoCAD writes ByBlock with ByLayer transparency as color number 0 (Air CAD F5-1); before 3.8.0-aircad.13 the reader read
/// it as opaque, so an unedited save wrote an explicit 0 % transparency into those entities.
/// </summary>
public class DwgEnColorTests
{
	[Theory]
	[InlineData(ACadVersion.AC1018)]
	[InlineData(ACadVersion.AC1024)]
	[InlineData(ACadVersion.AC1027)]
	[InlineData(ACadVersion.AC1032)]
	public void ByBlockColorKeepsItsTransparencyThroughAWrite(ACadVersion version)
	{
		CadDocument doc = new CadDocument();
		doc.Header.Version = version;
		doc.Entities.Add(new Line(XYZ.Zero, new XYZ(1, 0, 0)) { Color = Color.ByBlock });
		doc.Entities.Add(new Line(new XYZ(0, 1, 0), new XYZ(1, 1, 0)) { Color = Color.ByBlock, Transparency = Transparency.Opaque });
		doc.Entities.Add(new Line(new XYZ(0, 2, 0), new XYZ(1, 2, 0)) { Color = Color.ByBlock, Transparency = Transparency.ByBlock });
		doc.Entities.Add(new Line(new XYZ(0, 3, 0), new XYZ(1, 3, 0)) { Color = Color.ByBlock, Transparency = new Transparency(30) });

		MemoryStream stream = new MemoryStream();
		using (var wr = new DwgWriter(stream, doc))
		{
			wr.Write();
		}

		CadDocument read = DwgReader.Read(new MemoryStream(stream.ToArray()));
		Line[] lines = read.Entities.OfType<Line>().OrderBy(l => l.StartPoint.Y).ToArray();
		Assert.Equal(4, lines.Length);
		Assert.All(lines, l => Assert.True(l.Color.IsByBlock));
		Assert.True(lines[0].Transparency.IsByLayer);
		Assert.Equal(0, lines[1].Transparency.Value);
		Assert.True(lines[2].Transparency.IsByBlock);
		Assert.Equal(30, lines[3].Transparency.Value);
	}
}
