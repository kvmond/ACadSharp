using System.IO;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A layer's material (DXF 347, AC1021 and later) is written (Air CAD W1-4): the DWG writer wrote null for every layer, so a saved copy lost a
/// layer's material (AutoCAD shows Global for null, so a layer that held Global read alike and one that held another material lost it). A material
/// the written document does not hold (one of another document) is written null, so no handle names an object the file lacks. AC1018 stores none.
/// </summary>
public class LayerMaterialTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void ALayersMaterialSurvivesADwgRoundTrip(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var material = new Material("AIR_MATERIAL");
		document.Materials.Add(material);
		var other = new CadDocument();
		var foreign = new Material("AIR_FOREIGN");
		other.Materials.Add(foreign);
		document.Layers.Add(new Layer("WITH") { Material = material });
		document.Layers.Add(new Layer("WITHOUT"));
		document.Layers.Add(new Layer("FOREIGN") { Material = foreign });

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal(version >= ACadVersion.AC1021 ? "AIR_MATERIAL" : null, read.Layers["WITH"].Material?.Name);
		Assert.Null(read.Layers["WITHOUT"].Material);
		Assert.Null(read.Layers["FOREIGN"].Material);
	}
}
