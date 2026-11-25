using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A polyline's vertices keep the properties they were read with (Air CAD W1-19). Adding them to the polyline while the document was built gave each
/// the polyline's (MatchVerticesEntityProperties), so a vertex of color 1 under a polyline of color BYLAYER was saved BYLAYER (P2-POLYLINE of AC1018,
/// AC1024 and AC1027). Adding a vertex in code still gives it the polyline's properties.
/// </summary>
public class VertexPropertiesAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void AVertexKeepsTheColorItWasReadWith(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var polyline = new Polyline2D { MatchVerticesEntityProperties = false };
		polyline.Vertices.Add(new Vertex2D(new XYZ(0, 0, 0)) { Color = new Color(1) });
		polyline.Vertices.Add(new Vertex2D(new XYZ(10, 0, 0)));
		polyline.MatchVerticesEntityProperties = true;
		document.Entities.Add(polyline);

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray())).Entities.OfType<Polyline2D>().Single();

		Assert.Equal(new[] { (short)1, (short)256 }, read.Vertices.Select(v => v.Color.Index));
		Assert.True(read.MatchVerticesEntityProperties);
		read.Vertices.Add(new Vertex2D(new XYZ(20, 0, 0)) { Color = new Color(3) });
		Assert.Equal(256, read.Vertices.Last().Color.Index);
	}
}
