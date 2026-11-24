using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A dimension's stored actual measurement (DXF 42) survives a save while its geometry still gives it (Air CAD W1-17). The reader dropped it and the
/// writer wrote the measurement computed again, which differed in the last digits (AutoCAD's dump of a copy: 0.7853981633974483 became
/// 0.7853981633974484). A dimension whose geometry gives another measurement is written with the computed one, as before.
/// </summary>
public class DimensionMeasurementAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static byte[] Write(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return stream.ToArray();
	}

	private static CadDocument Read(byte[] bytes) => DwgReader.Read(new MemoryStream(bytes));

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheStoredMeasurementIsWrittenBackWhileTheGeometryGivesIt(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Entities.Add(new DimensionAligned(new XYZ(0, 0, 0), new XYZ(3, 4, 0)));
		var read = Read(Write(document));
		var dimension = read.Entities.OfType<DimensionAligned>().Single();
		Assert.Equal(dimension.Measurement, dimension.MeasurementAsRead);

		// A stored value a rounding away from the computed one is kept, bit for bit, through two saves.
		double stored = System.BitConverter.Int64BitsToDouble(System.BitConverter.DoubleToInt64Bits(dimension.Measurement) + 1);
		dimension.MeasurementAsRead = stored;
		var copy = Read(Write(read));
		var kept = copy.Entities.OfType<DimensionAligned>().Single();
		Assert.Equal(stored, kept.MeasurementAsRead);
		Assert.Equal(stored, Read(Write(copy)).Entities.OfType<DimensionAligned>().Single().MeasurementAsRead);

		// A dimension changed since reading is written with the computed measurement.
		kept.SecondPoint = new XYZ(6, 8, 0);
		var moved = Read(Write(copy)).Entities.OfType<DimensionAligned>().Single();
		Assert.Equal(moved.Measurement, moved.MeasurementAsRead);
		Assert.Equal(10, moved.MeasurementAsRead.Value, 9);
	}

	[Theory]
	[InlineData("AC1018")]
	[InlineData("AC1024")]
	[InlineData("AC1027")]
	[InlineData("AC1032")]
	public void AnAutoCadFilesMeasurementsSurviveASave(string version)
	{
		var document = DwgReader.Read(Path.Combine(TestVariables.SamplesFolder, $"sample_{version}.dwg"));
		var before = document.Entities.OfType<Dimension>().ToDictionary(d => d.Handle, d => d.MeasurementAsRead);
		Assert.NotEmpty(before);
		Assert.All(before.Values, v => Assert.NotNull(v));

		var copy = Read(Write(document));
		var after = copy.Entities.OfType<Dimension>().ToDictionary(d => d.Handle, d => d.MeasurementAsRead);
		foreach (var (handle, value) in before)
		{
			// Bit for bit where the library computes the same measurement; otherwise its own (a dimension kind it computes otherwise).
			var dimension = document.GetCadObject<Dimension>(handle);
			Assert.Equal(dimension.MeasurementToWrite(), after[handle]);
		}
	}
}
