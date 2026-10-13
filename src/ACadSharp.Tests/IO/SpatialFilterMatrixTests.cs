using System.IO;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// An XCLIP's SPATIAL_FILTER keeps its two matrices apart: the inverse block transform (the first 12 group 40 values in
/// DXF) and the clip boundary transform (the second 12). The DXF reader used to put both into the first matrix.
/// </summary>
public class SpatialFilterMatrixTests
{
	public static TheoryData<string> Formats => new() { "dwg", "dxf", "binary" };

	[Theory]
	[MemberData(nameof(Formats))]
	public void BothMatricesSurviveARoundTrip(string format)
	{
		var document = new CadDocument();
		var block = new BlockRecord("CLIPPED");
		block.Entities.Add(new Line(XYZ.Zero, new XYZ(10, 0, 0)));
		document.BlockRecords.Add(block);
		var insert = new Insert(block) { InsertPoint = new XYZ(100, 50, 0) };
		document.Entities.Add(insert);
		var inverse = Matrix4.Identity;
		(inverse.M00, inverse.M01, inverse.M03, inverse.M10, inverse.M11, inverse.M13) = (0, 1, -50, -1, 0, 100);
		var boundary = Matrix4.Identity;
		(boundary.M03, boundary.M13) = (5, 3);
		insert.SpatialFilter = new SpatialFilter(SpatialFilter.SpatialFilterEntryName)
		{
			BoundaryPoints = { new XY(102, 49), new XY(106, 57) },
			InverseInsertTransform = inverse,
			InsertTransform = boundary,
		};

		using var stream = new MemoryStream();
		if (format == "dwg") DwgWriter.Write(stream, document);
		else DxfWriter.Write(stream, document, binary: format == "binary");
		var read = format == "dwg" ? DwgReader.Read(new MemoryStream(stream.ToArray())) : DxfReader.Read(new MemoryStream(stream.ToArray()));

		var filter = Assert.IsType<Insert>(Assert.Single(read.Entities)).SpatialFilter;
		Assert.NotNull(filter);
		Assert.Equal(inverse, filter.InverseInsertTransform);
		Assert.Equal(boundary, filter.InsertTransform);
	}
}
