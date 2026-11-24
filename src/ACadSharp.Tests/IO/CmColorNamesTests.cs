using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A CMC colour keeps the names that follow it (Air CAD W1-6): the DWG reader read a colour's name and its colour book's name and dropped them, and
/// the writer wrote the colour byte 0, so a layer's colour or a text's background colour from a colour book kept its RGB and lost its names. The
/// names are written only while the colour is the one they name. AC1018 stores them too (R2004's CMC). DXF keeps a layer's (430, BOOK$COLOR).
/// </summary>
public class CmColorNamesTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static readonly Color Book = new Color(189, 236, 182);

	private static CadDocument Document(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		document.Layers.Add(new Layer("BOOK") { Color = Book, ColorNames = new CmColorNames(Book, "C534", "AIR TEST") });
		document.Layers.Add(new Layer("NAMED") { Color = Book, ColorNames = new CmColorNames(Book, "ONLY NAME", string.Empty) });
		document.Layers.Add(new Layer("RECOLOURED") { Color = new Color(1, 2, 3), ColorNames = new CmColorNames(Book, "C534", "AIR TEST") });
		document.Layers.Add(new Layer("PLAIN") { Color = Book });
		document.Entities.Add(new MText
		{
			Value = "AIR",
			InsertPoint = XYZ.Zero,
			BackgroundFillFlags = BackgroundFillFlags.UseBackgroundFillColor,
			BackgroundColor = Book,
			BackgroundColorNames = new CmColorNames(Book, "C707", "AIR TEST"),
		});
		return document;
	}

	private static (string Color, string Book) Names(CmColorNames names) => (names?.ColorName, names?.BookName);

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheNamesOfALayersAndABackgroundsColourSurviveADwgRoundTrip(ACadVersion version)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, Document(version));
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal(("C534", "AIR TEST"), Names(read.Layers["BOOK"].ColorNames));
		Assert.Equal(Book, read.Layers["BOOK"].Color);
		Assert.Equal((byte)3, read.Layers["BOOK"].ColorNames.Flags);
		Assert.Equal(("ONLY NAME", string.Empty), Names(read.Layers["NAMED"].ColorNames));
		Assert.Equal((byte)1, read.Layers["NAMED"].ColorNames.Flags);
		Assert.Null(read.Layers["RECOLOURED"].ColorNames);
		Assert.Null(read.Layers["PLAIN"].ColorNames);
		var mtext = read.Entities.OfType<MText>().Single();
		Assert.Equal(Book, mtext.BackgroundColor);
		Assert.Equal(("C707", "AIR TEST"), Names(mtext.BackgroundColorNames));
	}

	/// <summary>R2000's CMC is an index only: no names are written and the copy reads (the review of W1-6: the base overloads had no test).</summary>
	[Fact]
	public void BeforeR2004NoNamesAreWritten()
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, Document(ACadVersion.AC1015));
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Null(read.Layers["BOOK"].ColorNames);
		Assert.Null(read.Entities.OfType<MText>().Single().BackgroundColorNames);
	}

	/// <summary>
	/// A layer's names survive DXF as 430 beside the true colour's 420. Names of an index colour or names without text are not written (the review of
	/// W1-6, finding 2: a 430 without 420, or an empty 430).
	/// </summary>
	[Fact]
	public void ALayersColourNamesSurviveADxfRoundTrip()
	{
		var document = Document(ACadVersion.AC1032);
		var index = new Color(5);
		document.Layers.Add(new Layer("INDEXED") { Color = index, ColorNames = new CmColorNames(index, "C5", "AIR TEST") });
		document.Layers.Add(new Layer("EMPTY") { Color = Book, ColorNames = new CmColorNames(Book, string.Empty, string.Empty) });
		using var stream = new MemoryStream();
		DxfWriter.Write(stream, document);
		var read = DxfReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal(("C534", "AIR TEST"), Names(read.Layers["BOOK"].ColorNames));
		Assert.Equal(("ONLY NAME", string.Empty), Names(read.Layers["NAMED"].ColorNames));
		Assert.Null(read.Layers["RECOLOURED"].ColorNames);
		Assert.Null(read.Layers["PLAIN"].ColorNames);
		Assert.Null(read.Layers["INDEXED"].ColorNames);
		Assert.Null(read.Layers["EMPTY"].ColorNames);
	}

	[Fact]
	public void NamesNameOnlyTheirColour()
	{
		var names = new CmColorNames(Book, "C534", "AIR TEST");

		Assert.Same(names, CmColorNames.For(names, Book));
		Assert.Null(CmColorNames.For(names, new Color(1, 2, 3)));
		Assert.Null(CmColorNames.For(null, Book));
		Assert.Equal(("C534", "AIR TEST"), Names(CmColorNames.FromDxf("AIR TEST$C534", Book)));
		Assert.Equal(("C534", string.Empty), Names(CmColorNames.FromDxf("C534", Book)));
		Assert.Null(CmColorNames.FromDxf(string.Empty, Book));
	}
}
