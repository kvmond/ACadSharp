using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// Two kinds of entity a saved copy kept in another form (Air CAD W1-10 and W1-11). A TRACE was read as a SOLID and written as one, so the copy's
/// TRACEs became SOLIDs (P2-CURVES); it is now written with the type it was read with (DWG type 32, DXF TRACE) and stays a <see cref="Solid"/>
/// to everything else. A spline-fit 3D polyline was written with spline flags 0, so the copy lost the spline (AutoCAD's dump of P2-POLYLINE's 398:
/// 70 = 12, 75 = 6); the curve type is now read into <see cref="Polyline{T}.SmoothSurface"/> and written back.
/// </summary>
public class TraceAndSplinePolylineTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1032 };

	private static CadDocument RoundTrip(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	private static CadDocument Shapes(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Entities.Add(new Solid(XYZ.Zero, XYZ.AxisX, XYZ.AxisY, new XYZ(1, 1, 0)) { IsTrace = true });
		document.Entities.Add(new Solid(new XYZ(5, 0, 0), new XYZ(6, 0, 0), new XYZ(5, 1, 0), new XYZ(6, 1, 0)));
		document.Entities.Add(Spline3D(0, SmoothSurfaceType.Cubic));
		document.Entities.Add(Spline3D(10, SmoothSurfaceType.Quadratic));
		document.Entities.Add(new Polyline3D(new XYZ(0, 20, 0), new XYZ(1, 21, 1), new XYZ(2, 20, 2)));
		return document;
	}

	private static Polyline3D Spline3D(double y, SmoothSurfaceType type)
	{
		var polyline = new Polyline3D(new XYZ(0, y, 0), new XYZ(1, y + 1, 1), new XYZ(2, y, 2));
		polyline.Flags |= PolylineFlags.SplineFit;
		polyline.SmoothSurface = type;
		return polyline;
	}

	private static List<(ObjectType Type, double X)> Solids(CadDocument document) =>
		document.Entities.OfType<Solid>().Select(s => (s.ObjectType, s.FirstCorner.X)).OrderBy(s => s.X).ToList();

	private static List<(bool Spline, SmoothSurfaceType Type)> Polylines(CadDocument document) =>
		document.Entities.OfType<Polyline3D>().OrderBy(p => p.Vertices.First().Location.Y)
			.Select(p => (p.Flags.HasFlag(PolylineFlags.SplineFit), p.SmoothSurface)).ToList();

	[Theory]
	[MemberData(nameof(Versions))]
	public void ATraceAndASplineFitPolylineSurviveADwgRoundTrip(ACadVersion version)
	{
		var read = RoundTrip(Shapes(version));

		Assert.Equal(new (ObjectType, double)[] { (ObjectType.TRACE, 0d), (ObjectType.SOLID, 5d) }, Solids(read));
		Assert.All(read.Entities.OfType<Solid>(), s => Assert.Equal(DxfFileToken.EntitySolid, s.ObjectName));
		Assert.Equal(new (bool, SmoothSurfaceType)[] { (true, SmoothSurfaceType.Cubic), (true, SmoothSurfaceType.Quadratic), (false, SmoothSurfaceType.NoSmooth) }, Polylines(read));

		// Twice: what was read is written the same way.
		Assert.Equal(Solids(read), Solids(RoundTrip(read)));
		Assert.Equal(Polylines(read), Polylines(RoundTrip(read)));
	}

	[Fact]
	public void ATraceSurvivesADxfRoundTrip()
	{
		using var stream = new MemoryStream();
		DxfWriter.Write(stream, Shapes(ACadVersion.AC1032));
		var text = Encoding.ASCII.GetString(stream.ToArray());
		var lines = text.Split('\n').Select(l => l.Trim()).ToList();
		Assert.Equal(1, Enumerable.Range(0, lines.Count - 1).Count(i => lines[i] == "0" && lines[i + 1] == "TRACE"));
		Assert.Equal(1, Enumerable.Range(0, lines.Count - 1).Count(i => lines[i] == "0" && lines[i + 1] == "SOLID"));

		var read = DxfReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal(new (ObjectType, double)[] { (ObjectType.TRACE, 0d), (ObjectType.SOLID, 5d) }, Solids(read));
		Assert.Equal(new (bool, SmoothSurfaceType)[] { (true, SmoothSurfaceType.Cubic), (true, SmoothSurfaceType.Quadratic), (false, SmoothSurfaceType.NoSmooth) }, Polylines(read));
	}
}
