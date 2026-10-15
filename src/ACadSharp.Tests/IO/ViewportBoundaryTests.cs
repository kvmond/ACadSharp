using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A viewport clipped by an object (VPCLIP, status bit 65536, DXF 340): the DXF reader used to drop 340, so the viewport
/// came back with the clipping bit and no boundary, and a later save lost the clip.
/// </summary>
public class ViewportBoundaryTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void AClippedViewportKeepsItsBoundaryThroughDxf(bool binary)
	{
		var (document, boundaryHandle) = ClippedViewportDocument();

		using var stream = new MemoryStream();
		DxfWriter.Write(stream, document, binary);
		var read = DxfReader.Read(new MemoryStream(stream.ToArray()));

		AssertClipped(read, boundaryHandle);
	}

	[Fact]
	public void AClippedViewportKeepsItsBoundaryThroughDwg()
	{
		var (document, boundaryHandle) = ClippedViewportDocument();

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		AssertClipped(read, boundaryHandle);
	}

	private static (CadDocument Document, ulong BoundaryHandle) ClippedViewportDocument()
	{
		var document = new CadDocument();
		document.Header.Version = ACadVersion.AC1032;
		var boundary = new LwPolyline(new XY(10, 10), new XY(90, 10), new XY(50, 60)) { IsClosed = true };
		document.PaperSpace.Entities.Add(boundary);
		var viewport = new Viewport
		{
			Center = new XYZ(50, 35, 0), Width = 80, Height = 50, ViewCenter = new XY(0, 0), ViewHeight = 50,
			Boundary = boundary,
		};
		viewport.Status |= ViewportStatusFlags.NonRectangularClipping;
		document.PaperSpace.Entities.Add(viewport);
		return (document, boundary.Handle);
	}

	private static void AssertClipped(CadDocument read, ulong boundaryHandle)
	{
		var viewport = Assert.Single(read.PaperSpace.Entities.OfType<Viewport>(), v => v.Width == 80);
		Assert.True(viewport.Status.HasFlag(ViewportStatusFlags.NonRectangularClipping));
		var boundary = Assert.IsType<LwPolyline>(viewport.Boundary);
		Assert.Equal(boundaryHandle, boundary.Handle);
		Assert.Same(boundary, read.GetCadObject(boundaryHandle));
	}
}
