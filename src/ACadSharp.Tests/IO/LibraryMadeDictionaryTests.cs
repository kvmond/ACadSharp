using System.IO;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// An empty dictionary the library makes when it reads a file that lacks it is not written (Air CAD W1-15): ACAD_IMAGE_DICT and ACAD_PDFDEFINITIONS
/// came into every saved copy of a drawing without images or PDF underlays, which AutoCAD's dump of the copy listed in the named object dictionary
/// and the original's did not. A dictionary the file held, and one the library made that holds an entry by the time it is written, are written.
/// Whether the file held a dictionary shows when it is read back: the library marks the one it makes (<see cref="CadDictionary.MadeByLibrary"/>).
/// </summary>
public class LibraryMadeDictionaryTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1032 };

	private static byte[] Write(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return stream.ToArray();
	}

	private static CadDocument Read(byte[] bytes) => DwgReader.Read(new MemoryStream(bytes));

	private static bool Made(CadDocument document, string name) => document.RootDictionary.GetEntry<CadDictionary>(name).MadeByLibrary;

	[Theory]
	[MemberData(nameof(Versions))]
	public void AnEmptyDictionaryTheLibraryMadeOnReadingIsNotWritten(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		Assert.True(document.RootDictionary.Remove(CadDictionary.AcadImageDict));
		Assert.True(document.RootDictionary.Remove(CadDictionary.AcadPdfDefinitions));
		document.Entities.Add(new Line(XYZ.Zero, XYZ.AxisX));

		// The file lacks both: reading it, the library makes them.
		var read = Read(Write(document));
		Assert.True(Made(read, CadDictionary.AcadImageDict));
		Assert.True(Made(read, CadDictionary.AcadPdfDefinitions));

		// Saved again, the copy still lacks both, and so does a copy of the copy.
		var copy = Read(Write(read));
		Assert.True(Made(copy, CadDictionary.AcadImageDict));
		Assert.True(Made(copy, CadDictionary.AcadPdfDefinitions));
		Assert.True(Made(Read(Write(copy)), CadDictionary.AcadImageDict));
		Assert.Single(copy.Entities);
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void ADictionaryTheFileHeldOrThatHoldsAnEntryIsWritten(ACadVersion version)
	{
		// A new document holds both from the start: written, and read back from the file.
		var held = new CadDocument();
		held.Header.Version = version;
		var read = Read(Write(held));
		Assert.False(Made(read, CadDictionary.AcadImageDict));
		Assert.False(Made(read, CadDictionary.AcadPdfDefinitions));

		// Made on reading, then given an image: written with it.
		var bare = new CadDocument();
		bare.Header.Version = version;
		bare.RootDictionary.Remove(CadDictionary.AcadImageDict);
		var made = Read(Write(bare));
		Assert.True(Made(made, CadDictionary.AcadImageDict));
		made.Entities.Add(new RasterImage { Definition = new ImageDefinition("AIR_IMAGE") { FileName = "synthetic.png", Size = new XY(10, 20) }, Size = new XY(10, 20) });

		var withImage = Read(Write(made));
		Assert.False(Made(withImage, CadDictionary.AcadImageDict));
		Assert.Contains(withImage.ImageDefinitions, d => d.Name == "AIR_IMAGE");
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void AMadeDictionaryThatHeldAnEntryIsWrittenWhenEmptiedAgain(ACadVersion version)
	{
		// Made on reading, given an entry (a save may have written it), emptied again: the next save still writes it, so it loses nothing the
		// previous file held.
		var bare = new CadDocument();
		bare.Header.Version = version;
		bare.RootDictionary.Remove(CadDictionary.AcadPdfDefinitions);
		var read = Read(Write(bare));
		var made = read.RootDictionary.GetEntry<CadDictionary>(CadDictionary.AcadPdfDefinitions);
		Assert.True(made.MadeByLibrary);
		made.Add("AIR_ENTRY", new XRecord());
		Assert.False(made.MadeByLibrary);
		Assert.True(made.Remove("AIR_ENTRY"));
		Assert.False(made.MadeByLibrary);

		var again = Read(Write(read));
		Assert.False(Made(again, CadDictionary.AcadPdfDefinitions));
	}
}
