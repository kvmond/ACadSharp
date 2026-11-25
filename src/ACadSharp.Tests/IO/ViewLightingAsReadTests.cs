using System.IO;
using ACadSharp.IO;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A named view's lighting (R2007 and later) survives a save (Air CAD W1-21): the reader dropped the default lights bit, the lighting type, the
/// brightness, the contrast and the ambient colour, and the writer wrote AutoCAD's defaults, so S17-M-EXTENTS' ambient colour stored as the true
/// colour 51,51,51 came back as the index colour 250. A view made in code is written with the defaults, as before.
/// </summary>
public class ViewLightingAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static CadDocument SaveAndRead(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheLightingIsWrittenBackAsRead(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Views.Add(new View("W1_21_DEFAULT"));
		var view = new View("W1_21");
		view.LightingAsRead = (false, (byte)0, 0.25, 0.5, new Color(51, 51, 51), null);
		document.Views.Add(view);

		var read = SaveAndRead(document);

		var lighting = read.Views["W1_21"].LightingAsRead.Value;
		Assert.Equal((false, (byte)0, 0.25, 0.5), (lighting.UseDefaultLights, lighting.DefaultLighting, lighting.Brightness, lighting.Contrast));
		Assert.True(lighting.AmbientColor.IsTrueColor);
		Assert.Equal((51, 51, 51), ((int)lighting.AmbientColor.R, (int)lighting.AmbientColor.G, (int)lighting.AmbientColor.B));
		var defaults = read.Views["W1_21_DEFAULT"].LightingAsRead.Value;
		Assert.Equal((true, (byte)1, 0d, 0d), (defaults.UseDefaultLights, defaults.DefaultLighting, defaults.Brightness, defaults.Contrast));
		Assert.Equal((short)250, defaults.AmbientColor.Index);
	}
}
