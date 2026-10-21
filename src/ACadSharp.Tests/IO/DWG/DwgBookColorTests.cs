using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using System.IO;
using System.Linq;
using Xunit;

namespace ACadSharp.Tests.IO.DWG;

/// <summary>
/// The color of an entity with a color-book color (DXF 430). A DWG holds the book as a reference in the handle stream (flags 0xC000) and
/// no RGB; a DXF holds the book's RGB in 62/420 beside the reference. Before 3.8.0-aircad.15 an entity read from a DXF carried that RGB as
/// its own color, and the DWG writer wrote it after the flags too: the reader read none, took the RGB for the transparency or the next
/// field, and the entity's data was shifted (the DXF conversion of a drawing with book colors lost its lines, Air CAD F5-12).
/// </summary>
public class DwgBookColorTests
{
	private static readonly Color BookRgb = new(51, 153, 102);

	private static CadDocument Source(ACadVersion version, bool dxfForm)
	{
		CadDocument doc = new CadDocument(version);
		doc.Header.Version = version;
		var book = new BookColor("RAL CLASSIC$RAL 6024") { Color = BookRgb };
		doc.Colors.Add(book);
		Transparency[] transparencies = [Transparency.ByLayer, Transparency.ByBlock, Transparency.Opaque, new Transparency(30)];
		for (var i = 0; i < transparencies.Length; i++)
		{
			doc.Entities.Add(new Line(new XYZ(0, i, 0), new XYZ(10 + i, i, 0))
			{
				// The form a DXF reader gave before the fix keeps the RGB as the entity's color; a DWG reader leaves the placeholder.
				Color = dxfForm ? BookRgb : Color.ByBlock,
				BookColor = book,
				Transparency = transparencies[i],
			});
		}
		return doc;
	}

	private static void AssertLines(CadDocument read)
	{
		Line[] lines = read.Entities.OfType<Line>().OrderBy(l => l.StartPoint.Y).ToArray();
		Assert.Equal(4, lines.Length);
		for (var i = 0; i < lines.Length; i++)
		{
			Assert.Equal(new XYZ(0, i, 0), lines[i].StartPoint);
			Assert.Equal(new XYZ(10 + i, i, 0), lines[i].EndPoint);
			Assert.Equal("RAL CLASSIC$RAL 6024", lines[i].BookColor?.Name);
			Assert.Equal(BookRgb, lines[i].BookColor!.Color);
			Assert.True(lines[i].Color.IsByBlock);
		}
		Assert.True(lines[0].Transparency.IsByLayer);
		Assert.True(lines[1].Transparency.IsByBlock);
		Assert.Equal(0, lines[2].Transparency.Value);
		Assert.Equal(30, lines[3].Transparency.Value);
	}

	[Theory]
	[InlineData(ACadVersion.AC1018, false)]
	[InlineData(ACadVersion.AC1018, true)]
	[InlineData(ACadVersion.AC1024, true)]
	[InlineData(ACadVersion.AC1027, true)]
	[InlineData(ACadVersion.AC1032, false)]
	[InlineData(ACadVersion.AC1032, true)]
	public void ABookColorWritesNoRgbWhateverTheEntitysColorIs(ACadVersion version, bool dxfForm)
	{
		MemoryStream stream = new MemoryStream();
		using (var wr = new DwgWriter(stream, Source(version, dxfForm)))
		{
			wr.Write();
		}

		AssertLines(DwgReader.Read(new MemoryStream(stream.ToArray())));
	}
}
