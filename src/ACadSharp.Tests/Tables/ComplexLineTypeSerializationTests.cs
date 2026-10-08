using ACadSharp.IO;
using ACadSharp.IO.DWG;
using System.Collections.Generic;
using ACadSharp.Tables;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace ACadSharp.Tests.Tables;

/// <summary>Synthetic fixtures only: no third-party drawing or font bytes.</summary>
public class ComplexLineTypeSerializationTests
{
	private static CadDocument Document(ACadVersion version, string codePage, params string[] text)
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		var document = new CadDocument(version); document.Header.CodePage = codePage;
		var style = new TextStyle("TEXT_STYLE") { Filename = "synthetic.ttf" }; document.TextStyles.Add(style);
		var line = new LineType("COMPLEX");
		foreach (var value in text)
			line.AddSegment(new LineType.Segment { Length = -.25, Flags = (LineTypeShapeFlags)10,
				Text = value, Style = style, ShapeNumber = 123, Scale = .7, Rotation = .25, Offset = new CSMath.XY(.3, -.4) });
		document.LineTypes.Add(line);
		return document;
	}

	private static byte[] Write(CadDocument document, bool dxf, bool binary = false)
	{
		using var stream = new MemoryStream();
		if (dxf) DxfWriter.Write(stream, document, binary); else DwgWriter.Write(stream, document);
		return stream.ToArray();
	}
	private static CadDocument Read(byte[] bytes, bool dxf)
	{
		using var stream = new MemoryStream(bytes);
		return dxf ? DxfReader.Read(stream) : DwgReader.Read(stream);
	}

	[Theory]
	[InlineData(ACadVersion.AC1014, "ANSI_1252", false)]
	[InlineData(ACadVersion.AC1015, "ANSI_1252", false)]
	[InlineData(ACadVersion.AC1024, "ANSI_1252", false)]
	[InlineData(ACadVersion.AC1027, "ANSI_949", false)]
	[InlineData(ACadVersion.AC1018, "ANSI_1252", false)]
	[InlineData(ACadVersion.AC1018, "ANSI_949", false)]
	[InlineData(ACadVersion.AC1032, "ANSI_1252", false)]
	[InlineData(ACadVersion.AC1032, "ANSI_949", false)]
	[InlineData(ACadVersion.AC1018, "ANSI_949", true)]
	[InlineData(ACadVersion.AC1032, "ANSI_949", true)]
	public void TextEmptyOffsetsStylesAndSourceSurviveTwoGenerations(ACadVersion version, string codePage, bool dxf)
	{
		var expected = new[] { "AB", "", codePage == "ANSI_949" || version >= ACadVersion.AC1021 ? "한글A" : "éàA", "", "LAST" };
		var document = Document(version, codePage, expected);
		for (var generation = 0; generation < 2; generation++)
		{
			var originals = document.LineTypes["COMPLEX"].Segments.ToArray();
			var before = originals.Select(s => (s.Text, s.ShapeNumber, s.Style, s.Flags, s.Scale, s.Rotation, s.Offset, s.Length)).ToArray();
			var bytes = Write(document, dxf);
			Assert.Equal(before, originals.Select(s => (s.Text, s.ShapeNumber, s.Style, s.Flags, s.Scale, s.Rotation, s.Offset, s.Length)).ToArray());
			document = Read(bytes, dxf);
			var segments = document.LineTypes["COMPLEX"].Segments.ToArray();
			Assert.Equal(expected, segments.Select(s => s.Text));
			foreach (var segment in segments)
			{
				Assert.Same(document.TextStyles["TEXT_STYLE"], segment.Style);
				Assert.Same(segment.Style, document.GetCadObject(segment.Style.Handle));
				Assert.Equal(10, (int)segment.Flags); Assert.Equal(.7, segment.Scale); Assert.Equal(.25, segment.Rotation, 12);
				Assert.Equal(new CSMath.XY(.3, -.4), segment.Offset); Assert.Equal(-.25, segment.Length);
			}
		}
	}

	[Theory]
	[InlineData(ACadVersion.AC1014, 254)]
	[InlineData(ACadVersion.AC1018, 255)]
	[InlineData(ACadVersion.AC1032, 255)]
	public void ExactTextAreaLimitWritesButOneCharacterMoreRefusesWithoutChangingSegments(ACadVersion version, int characters)
	{
		var document = Document(version, "ANSI_1252", new string('A', characters));
		var segment = document.LineTypes["COMPLEX"].Segments.Single();
		var before = segment.ShapeNumber;
		Assert.Equal(segment.Text, Read(Write(document, false), false).LineTypes["COMPLEX"].Segments.Single().Text);
		Assert.Equal(before, segment.ShapeNumber);
		segment.Text += "B";
		Assert.ThrowsAny<Exception>(() => Write(document, false));
		Assert.Equal(before, segment.ShapeNumber); Assert.Equal(new string('A', characters) + "B", segment.Text);
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void DxfTextAndBinaryPreserveSeparateTextAndShapeStyles(bool binary)
	{
		var document = Document(ACadVersion.AC1032, "ANSI_949", "", "한글😀");
		var shape = new TextStyle("SHAPE_STYLE") { Flags = StyleFlags.IsShape, Filename = "synthetic.shx" };
		document.TextStyles.Add(shape);
		document.LineTypes["COMPLEX"].AddSegment(new LineType.Segment { Flags = (LineTypeShapeFlags)12, ShapeNumber = 65, Style = shape, Length = 1 });
		var shapeHandle = shape.Handle;
		for (var generation = 0; generation < 2; generation++)
		{
			document = Read(Write(document, true, binary), true);
			var segments = document.LineTypes["COMPLEX"].Segments.ToArray();
			Assert.Equal("", segments[0].Text); Assert.Equal("한글😀", segments[1].Text);
			Assert.Same(document.TextStyles["TEXT_STYLE"], segments[0].Style);
			Assert.Same(document.TextStyles["TEXT_STYLE"], segments[1].Style);
			Assert.Equal(shapeHandle, segments[2].Style.Handle); Assert.Equal(65, segments[2].ShapeNumber);
			Assert.Equal(12, (int)segments[2].Flags); Assert.Equal("synthetic.shx", segments[2].Style.Filename);
			Assert.True(segments[2].Style.IsShapeFile); Assert.Same(segments[2].Style, document.GetCadObject(shapeHandle));
			// The same registered references must survive the real DXF-to-DWG path too.
			document = Read(Write(document, false), false);
			Assert.Equal("한글😀", document.LineTypes["COMPLEX"].Segments.ElementAt(1).Text);
		}
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)]
	[InlineData(ACadVersion.AC1032)]
	public void EveryTextHasAnOffsetAndTerminatorIncludingEmptyText(ACadVersion version)
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		var encoding = Encoding.GetEncoding(949);
		var text = new[] { "", "A", "", "한", "", "Z" };
		var segments = text.Select(t => new LineType.Segment { Flags = LineTypeShapeFlags.Text, Text = t, ShapeNumber = 77 }).ToArray();
		var area = DwgLineTypeText.Prepare(segments, version, encoding, out var offsets);
		Assert.Equal(text, offsets.Select(i => DwgLineTypeText.Read(area, i, version, encoding)));
		Assert.Equal(offsets.Length, offsets.Distinct().Count()); Assert.All(segments, s => Assert.Equal(77, s.ShapeNumber));
		Assert.Equal(version < ACadVersion.AC1021 ? 256 : 512, area.Length);
		if (version >= ACadVersion.AC1021) Assert.All(offsets, i => Assert.Equal(0, i % 2));
	}

	[Fact]
	public void DbcsExactByteLimitAndMultiSegmentAggregateLimitAreBounded()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		var document = Document(ACadVersion.AC1018, "ANSI_949", "A" + new string('한', 127));
		var segment = document.LineTypes["COMPLEX"].Segments.Single();
		Assert.Equal(segment.Text, Read(Write(document, false), false).LineTypes["COMPLEX"].Segments.Single().Text);
		Assert.Equal(123, segment.ShapeNumber);
		document.LineTypes["COMPLEX"].AddSegment(new LineType.Segment { Flags = LineTypeShapeFlags.Text, Text = "", Style = segment.Style, ShapeNumber = 99 });
		Assert.Throws<InvalidDataException>(() => Write(document, false));
		Assert.Equal(new short[] { 123, 99 }, document.LineTypes["COMPLEX"].Segments.Select(s => s.ShapeNumber));
	}

	[Theory]
	[InlineData(ACadVersion.AC1018, "한")]
	[InlineData(ACadVersion.AC1018, "A\0B")]
	[InlineData(ACadVersion.AC1032, "A\0B")]
	public void UnrepresentableOrEmbeddedNullTextRefusesWithoutMutatingSource(ACadVersion version, string text)
	{
		var document = Document(version, "ANSI_1252", "FIRST", text);
		var original = document.LineTypes["COMPLEX"].Segments.Select(s => (s.Text, s.ShapeNumber, s.Style)).ToArray();
		Assert.ThrowsAny<Exception>(() => Write(document, false));
		Assert.Equal(original, document.LineTypes["COMPLEX"].Segments.Select(s => (s.Text, s.ShapeNumber, s.Style)).ToArray());
	}

	[Fact]
	public void InvalidUtf16WriterAndDecoderNeverUseReplacementCharacters()
	{
		var document = Document(ACadVersion.AC1032, "ANSI_1252", "FIRST", "\uD800");
		Assert.Throws<EncoderFallbackException>(() => Write(document, false));
		Assert.Equal(new short[] { 123, 123 }, document.LineTypes["COMPLEX"].Segments.Select(s => s.ShapeNumber));
		var area = new byte[512]; area[1] = 0xD8;
		Assert.Throws<DecoderFallbackException>(() => DwgLineTypeText.Read(area, 0, ACadVersion.AC1032, Encoding.ASCII));
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)]
	[InlineData(ACadVersion.AC1032)]
	public void InvalidOffsetsMissingTerminatorsAndTruncatedAreasRefuse(ACadVersion version)
	{
		int size = version < ACadVersion.AC1021 ? 256 : 512;
		var area = new byte[size];
		Assert.Throws<InvalidDataException>(() => DwgLineTypeText.Read(area, -1, version, Encoding.ASCII));
		Assert.Throws<InvalidDataException>(() => DwgLineTypeText.Read(area, size, version, Encoding.ASCII));
		Assert.Throws<InvalidDataException>(() => DwgLineTypeText.Read(new byte[size - 1], 0, version, Encoding.ASCII));
		Assert.Throws<InvalidDataException>(() => DwgLineTypeText.Read(Enumerable.Repeat((byte)65, size).ToArray(), 0, version, Encoding.ASCII));
		if (size == 512) Assert.Throws<InvalidDataException>(() => DwgLineTypeText.Read(area, 1, version, Encoding.ASCII));
		else
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); area[0] = 0x81;
			Assert.Throws<DecoderFallbackException>(() => DwgLineTypeText.Read(area, 0, version, Encoding.GetEncoding(949)));
		}
	}

	[Theory]
	[InlineData(0, "missing", false)]
	[InlineData(2, "zero", false)]
	[InlineData(4, "zero", false)]
	[InlineData(2, "missing", true)]
	[InlineData(4, "wrong-type", true)]
	public void MissingStyleReportsAnErrorOnlyForANonzeroComplexReference(int flags, string reference, bool error)
	{
		var document = Document(ACadVersion.AC1032, "ANSI_1252", "A");
		var lines = Encoding.UTF8.GetString(Write(document, true)).Replace("\r\n", "\n").Split('\n');
		var found = false;
		for (int i = 0; i + 1 < lines.Length; i += 2)
		{
			if (lines[i].Trim() == "74") lines[i + 1] = flags.ToString();
			if (lines[i].Trim() != "340") continue;
			lines[i + 1] = reference == "zero" ? "0" : reference == "wrong-type" ? document.Layers["0"].Handle.ToString("X") : "FFFFFFF";
			found = true;
		}
		Assert.True(found);
		var notifications = new List<NotificationEventArgs>();
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
		var read = DxfReader.Read(stream, (_, e) => notifications.Add(e));
		Assert.Null(read.LineTypes["COMPLEX"].Segments.Single().Style);
		var styleErrors = notifications.Where(n => n.NotificationType == NotificationType.Error && n.Message.Contains("non-STYLE")).ToArray();
		Assert.Equal(error ? 1 : 0, styleErrors.Length);
		if (error) Assert.IsType<InvalidDataException>(styleErrors[0].Exception);
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)]
	[InlineData(ACadVersion.AC1032)]
	public void NumericAndShapeSegmentsKeepShapeNumbersAndSegmentCountBounds(ACadVersion version)
	{
		var segments = Enumerable.Range(0, 255).Select(i => new LineType.Segment { ShapeNumber = (short)i }).ToArray();
		var area = DwgLineTypeText.Prepare(segments, version, Encoding.ASCII, out var offsets);
		Assert.Equal(segments.Select(s => s.ShapeNumber), offsets);
		if (version >= ACadVersion.AC1021) Assert.Null(area); else Assert.Equal(256, area.Length);
		Assert.Throws<InvalidDataException>(() => DwgLineTypeText.Prepare(segments.Concat(new[] { new LineType.Segment() }).ToArray(), version, Encoding.ASCII, out _));
		var document = Document(version, "ANSI_1252", "A");
		var shape = new TextStyle("SHAPES") { Flags = StyleFlags.IsShape, Filename = "synthetic.shx" }; document.TextStyles.Add(shape);
		var segment = new LineType.Segment { Flags = (LineTypeShapeFlags)13, ShapeNumber = 65, Style = shape, Length = 1 };
		document.LineTypes["COMPLEX"].AddSegment(segment);
		for (int generation = 0; generation < 2; generation++)
		{
			document = Read(Write(document, false), false);
			var read = document.LineTypes["COMPLEX"].Segments.Last();
			Assert.Equal(65, read.ShapeNumber); Assert.Equal(13, (int)read.Flags); Assert.True(read.Style.IsShapeFile);
			Assert.Same(read.Style, document.GetCadObject(read.Style.Handle));
		}
		Assert.Equal(65, segment.ShapeNumber);
	}
}
