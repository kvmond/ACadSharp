using System.IO;
using ACadSharp.IO;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A layer's "frozen in new viewports" flag (AutoCAD's VPDFLT, DXF 70 bit 2) is its own: the R2000+ DWG writer used to
/// write it from the layer's frozen flag, so a thawed layer lost it and a frozen one gained it.
/// </summary>
public class LayerFrozenInNewViewportsTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheFlagSurvivesADwgRoundTripApartFromFrozen(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Layers.Add(new Layer("THAWED-VPDFLT") { Flags = LayerFlags.FrozenNewViewports });
		document.Layers.Add(new Layer("FROZEN-ONLY") { Flags = LayerFlags.Frozen });
		document.Layers.Add(new Layer("BOTH") { Flags = LayerFlags.Frozen | LayerFlags.FrozenNewViewports });

		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal(LayerFlags.FrozenNewViewports, read.Layers["THAWED-VPDFLT"].Flags & (LayerFlags.Frozen | LayerFlags.FrozenNewViewports));
		Assert.Equal(LayerFlags.Frozen, read.Layers["FROZEN-ONLY"].Flags & (LayerFlags.Frozen | LayerFlags.FrozenNewViewports));
		Assert.Equal(LayerFlags.Frozen | LayerFlags.FrozenNewViewports, read.Layers["BOTH"].Flags & (LayerFlags.Frozen | LayerFlags.FrozenNewViewports));
	}
}
