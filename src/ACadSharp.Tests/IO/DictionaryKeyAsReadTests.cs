using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp.IO;
using ACadSharp.Objects;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A DWG file's dictionary keys survive a save where they are not the entries' names (Air CAD W1-16). The library keys a dictionary's entries by
/// their names and wrote those names, so AutoCAD's anonymous scale-list keys (A0, A1, ...) came back as "1:1", "1:2", ... and the multiline
/// style dictionary's "Standard" as "STANDARD". An entry renamed since reading is written under its new name, as before.
/// </summary>
public class DictionaryKeyAsReadTests
{
	public static TheoryData<string> AutoCadSamples => new() { "AC1018", "AC1024", "AC1027", "AC1032" };

	private static byte[] Write(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return stream.ToArray();
	}

	private static CadDocument Read(byte[] bytes) => DwgReader.Read(new MemoryStream(bytes));

	private static Dictionary<ulong, string> Keys(CadDictionary dictionary) => dictionary.ToDictionary(e => e.Handle, dictionary.WrittenKey);

	/// <summary>The keys (group 3) of the object with handle <paramref name="handle"/> in an ASCII DXF.</summary>
	private static List<string> DxfKeys(string path, ulong handle)
	{
		var lines = File.ReadAllLines(path).Select(l => l.Trim()).ToArray();
		var keys = new List<string>();
		string wanted = handle.ToString("X");
		for (int i = 0; i + 1 < lines.Length; i += 2)
		{
			if (lines[i] != "0" || lines[i + 1] != "DICTIONARY") continue;
			int j = i + 2;
			bool ours = false;
			for (; j + 1 < lines.Length && lines[j] != "0"; j += 2)
			{
				if (lines[j] == "5") ours = lines[j + 1] == wanted;
				else if (ours && lines[j] == "3") keys.Add(lines[j + 1]);
			}
			if (ours) return keys;
			i = j - 2;
		}

		return keys;
	}

	[Theory]
	[MemberData(nameof(AutoCadSamples))]
	public void AnAutoCadFilesScaleListAndMultilineStyleKeysSurviveASave(string version)
	{
		string folder = TestVariables.SamplesFolder;
		var document = DwgReader.Read(Path.Combine(folder, $"sample_{version}.dwg"));
		var scales = document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadScaleList);
		var styles = document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadMLineStyle);
		Assert.NotNull(scales);
		Assert.NotNull(styles);

		// The keys read are the DXF's (AutoCAD wrote both), and the scale list's are not the scales' names.
		string dxf = Path.Combine(folder, $"sample_{version}_ascii.dxf");
		var scaleKeys = Keys(scales);
		Assert.Equal(DxfKeys(dxf, scales.Handle).OrderBy(k => k, System.StringComparer.Ordinal), scaleKeys.Values.OrderBy(k => k, System.StringComparer.Ordinal));
		Assert.Contains(scales, e => scales.WrittenKey(e) != e.Name);
		var styleKeys = Keys(styles);
		Assert.Equal(DxfKeys(dxf, styles.Handle).OrderBy(k => k, System.StringComparer.Ordinal), styleKeys.Values.OrderBy(k => k, System.StringComparer.Ordinal));

		var copy = Read(Write(document));
		Assert.Equal(scaleKeys, Keys(copy.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadScaleList)));
		Assert.Equal(styleKeys, Keys(copy.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadMLineStyle)));
	}

	[Fact]
	public void AnEntryRenamedSinceReadingIsWrittenUnderItsNewName()
	{
		var document = DwgReader.Read(Path.Combine(TestVariables.SamplesFolder, "sample_AC1032.dwg"));
		var scales = document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadScaleList);
		var renamed = scales.First(e => scales.WrittenKey(e) != e.Name);
		ulong handle = renamed.Handle;
		renamed.Name = "Air CAD 1:3";
		Assert.Equal("Air CAD 1:3", scales.WrittenKey(renamed));

		var copy = Read(Write(document)).RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadScaleList);
		var entry = copy.Single(e => e.Handle == handle);
		Assert.Equal("Air CAD 1:3", entry.Name);
		Assert.Equal("Air CAD 1:3", copy.WrittenKey(entry));
	}

	[Fact]
	public void ANewDocumentsKeysAreItsEntriesNames()
	{
		var document = new CadDocument();
		document.Header.Version = ACadVersion.AC1032;
		foreach (var dictionary in new[] { document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadScaleList), document.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadMLineStyle) })
		{
			Assert.All(dictionary, e => Assert.Equal(e.Name, dictionary.WrittenKey(e)));
		}

		var copy = Read(Write(document));
		var styles = copy.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadMLineStyle);
		Assert.All(styles, e => Assert.Equal(e.Name, styles.WrittenKey(e)));
	}
}
