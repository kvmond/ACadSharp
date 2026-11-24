using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ACadSharp.IO;
using ACadSharp.Objects;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// Six header values a DWG file holds survive a save (Air CAD W1-14). The library read TILEMODELIGHTSYNCH and the bit before INTERFERECOLOR
/// (REALWORLDSCALE) and dropped them, read INTERSECTIONCOLOR into INTERFERECOLOR and wrote INTERFERECOLOR's index into its place, wrote CMATERIAL, DICTIONARY_PLOTSETTINGS and
/// DICTIONARY_VISUALSTYLE as null handles, and for R2013 and later read DICTIONARY_VISUALSTYLE from the unknown handle after it. AutoCAD's
/// files hold 1, 1, 257 and the handles of the ByLayer material and of the root dictionary's two entries; every saved copy held 48 ('0'), 0, 1 and
/// null handles. Values the file held are written back; a new document is written as before.
/// </summary>
public class HeaderKeptAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	public static TheoryData<string> AutoCadSamples => new() { "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

	private static byte[] Write(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return stream.ToArray();
	}

	private static CadDocument Read(byte[] bytes) => DwgReader.Read(new MemoryStream(bytes));

	private static bool R2007Plus(ACadVersion version) => version >= ACadVersion.AC1021;

	/// <summary>The first value of each header variable of an ASCII DXF, by name without the dollar sign.</summary>
	private static Dictionary<string, string> DxfHeader(string path)
	{
		var lines = File.ReadAllLines(path);
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		for (int i = 0; i + 3 < lines.Length; i += 2)
		{
			if (lines[i].Trim() == "0" && lines[i + 1].Trim() == "ENDSEC") break;
			if (lines[i].Trim() == "9") values.TryAdd(lines[i + 1].Trim().TrimStart('$'), lines[i + 3].Trim());
		}

		return values;
	}

	[Theory]
	[MemberData(nameof(AutoCadSamples))]
	public void AnAutoCadFilesValuesAreReadAsItsDxfHoldsThemAndSurviveASave(string version)
	{
		string folder = TestVariables.SamplesFolder;
		var document = DwgReader.Read(Path.Combine(folder, $"sample_{version}.dwg"));
		var dxf = DxfHeader(Path.Combine(folder, $"sample_{version}_ascii.dxf"));
		var header = document.Header;

		Assert.Equal(short.Parse(dxf["INTERSECTIONCOLOR"], CultureInfo.InvariantCulture), header.IntersectionColorAsRead);
		var plotSettings = document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadPlotSettings);
		Assert.NotNull(plotSettings);
		Assert.Same(plotSettings, header.PlotSettingsDictionaryAsRead);
		if (version == "AC1018")
		{
			// INTERFERECOLOR is not in an R2004 file: it keeps its default, apart from INTERSECTIONCOLOR.
			Assert.Equal((short)1, header.InterfereColor.Index);
			Assert.Null(header.TileModeLightSynchAsRead);
			Assert.Null(header.RealWorldScaleAsRead);
			Assert.Null(header.CurrentMaterialAsRead);
			Assert.Null(header.VisualStyleDictionaryAsRead);
		}
		else
		{
			Assert.Equal(byte.Parse(dxf["TILEMODELIGHTSYNCH"], CultureInfo.InvariantCulture), header.TileModeLightSynchAsRead);
			Assert.Equal(dxf["REALWORLDSCALE"] == "1", header.RealWorldScaleAsRead);
			Assert.Equal(ulong.Parse(dxf["CMATERIAL"], NumberStyles.HexNumber, CultureInfo.InvariantCulture), header.CurrentMaterialAsRead?.Handle);
			Assert.Equal("ByLayer", header.CurrentMaterialAsRead.Name, ignoreCase: true);
			var visualStyles = document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadVisualStyle);
			Assert.NotNull(visualStyles);
			Assert.Same(visualStyles, header.VisualStyleDictionaryAsRead);
		}

		// The library writes no AC1021 file: saved as AC1032, as Air CAD saves one.
		if (version == "AC1021") header.Version = ACadVersion.AC1032;
		var copy = Read(Write(document)).Header;
		Assert.Equal(header.IntersectionColorAsRead, copy.IntersectionColorAsRead);
		Assert.Equal(header.PlotSettingsDictionaryAsRead.Handle, copy.PlotSettingsDictionaryAsRead?.Handle);
		Assert.Equal(header.TileModeLightSynchAsRead, copy.TileModeLightSynchAsRead);
		Assert.Equal(header.RealWorldScaleAsRead, copy.RealWorldScaleAsRead);
		Assert.Equal(header.CurrentMaterialAsRead?.Handle, copy.CurrentMaterialAsRead?.Handle);
		Assert.Equal(header.VisualStyleDictionaryAsRead?.Handle, copy.VisualStyleDictionaryAsRead?.Handle);
	}

	[Fact]
	public void AnAc1032FileSavedAsAc1018KeepsItsIntersectionAndInterfereColorsApart()
	{
		var document = DwgReader.Read(Path.Combine(TestVariables.SamplesFolder, "sample_AC1032.dwg"));
		short interfere = document.Header.InterfereColor.Index;
		short? intersection = document.Header.IntersectionColorAsRead;
		Assert.NotEqual(intersection, interfere);
		document.Header.Version = ACadVersion.AC1018;

		var header = Read(Write(document)).Header;
		Assert.Equal(intersection, header.IntersectionColorAsRead);
		Assert.Equal(interfere, header.InterfereColor.Index);
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheValuesAFileHeldSurviveTwoSaves(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var read = Read(Write(document));
		read.Header.TileModeLightSynchAsRead = 1;
		read.Header.RealWorldScaleAsRead = true;
		read.Header.IntersectionColorAsRead = 257;
		read.Header.CurrentMaterialAsRead = read.Materials["ByLayer"];
		read.Header.PlotSettingsDictionaryAsRead = read.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadPlotSettings);
		read.Header.VisualStyleDictionaryAsRead = read.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadVisualStyle);
		Assert.NotNull(read.Header.CurrentMaterialAsRead);
		Assert.NotNull(read.Header.PlotSettingsDictionaryAsRead);
		Assert.NotNull(read.Header.VisualStyleDictionaryAsRead);

		var copy = Read(Write(read));
		var twice = Read(Write(copy));
		foreach (var header in new[] { copy.Header, twice.Header })
		{
			Assert.Equal((short)257, header.IntersectionColorAsRead);
			Assert.Equal(read.Header.PlotSettingsDictionaryAsRead.Handle, header.PlotSettingsDictionaryAsRead?.Handle);
			if (R2007Plus(version))
			{
				Assert.Equal((byte)1, header.TileModeLightSynchAsRead);
				Assert.True(header.RealWorldScaleAsRead);
				Assert.Equal(read.Header.CurrentMaterialAsRead.Handle, header.CurrentMaterialAsRead?.Handle);
				Assert.Equal(read.Header.VisualStyleDictionaryAsRead.Handle, header.VisualStyleDictionaryAsRead?.Handle);
			}
			else
			{
				Assert.Null(header.TileModeLightSynchAsRead);
				Assert.Null(header.RealWorldScaleAsRead);
				Assert.Null(header.CurrentMaterialAsRead);
				Assert.Null(header.VisualStyleDictionaryAsRead);
			}
		}
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void ANewDocumentIsWrittenAsBefore(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var header = Read(Write(document)).Header;

		Assert.Equal(document.Header.InterfereColor.Index, header.IntersectionColorAsRead);
		Assert.Null(header.PlotSettingsDictionaryAsRead);
		Assert.Null(header.CurrentMaterialAsRead);
		Assert.Null(header.VisualStyleDictionaryAsRead);
		if (R2007Plus(version))
		{
			Assert.Equal((byte)'0', header.TileModeLightSynchAsRead);
			Assert.False(header.RealWorldScaleAsRead);
		}
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void AnObjectNoLongerInTheDocumentIsWrittenNull(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var read = Read(Write(document));
		var other = Read(Write(document));
		// Another document's objects, and a dictionary taken out of the root dictionary, are not this document's to name.
		read.Header.CurrentMaterialAsRead = other.Materials["ByLayer"];
		read.Header.VisualStyleDictionaryAsRead = other.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadVisualStyle);
		var plotSettings = read.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadPlotSettings);
		read.Header.PlotSettingsDictionaryAsRead = plotSettings;
		ulong removed = plotSettings.Handle;
		Assert.NotEqual(0UL, removed);
		Assert.True(read.RootDictionary.Remove(CadDictionary.AcadPlotSettings));

		var header = Read(Write(read)).Header;
		Assert.Null(header.CurrentMaterialAsRead);
		Assert.Null(header.VisualStyleDictionaryAsRead);
		// The file has no dictionary of that name now: the reader makes one, which the header does not name.
		Assert.Null(header.PlotSettingsDictionaryAsRead);
	}
}
