using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.XData;
using CSMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>Synthetic reference fixtures; no third-party drawing or font data.</summary>
public class XDataReferenceSerializationTests
{
	private const string App = "XDATA_REFS";
	private const ulong Missing = 0xFEDCBA9876543210;

	private static CadDocument document(ACadVersion version)
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		var doc = new CadDocument(version); doc.Header.CodePage = "ANSI_949";
		doc.Layers.Add(new Layer("한글_LAYER"));
		doc.Entities.Add(new Line(XYZ.Zero, XYZ.AxisX));
		doc.Entities.Add(new Line(XYZ.AxisY, XYZ.AxisZ));
		return doc;
	}

	private static byte[] write(CadDocument doc, int format)
	{
		using var stream = new MemoryStream();
		if (format == 0) DwgWriter.Write(stream, doc); else DxfWriter.Write(stream, doc, format == 2);
		return stream.ToArray();
	}

	private static CadDocument read(byte[] bytes, int format)
	{
		using var stream = new MemoryStream(bytes);
		return format == 0 ? DwgReader.Read(stream) : DxfReader.Read(stream);
	}

	private static (DxfCode Code, string Value)[] values(Entity entity) => entity.ExtendedData.Get(App).Records
		.Select(r => (r.Code, r is ExtendedDataBinaryChunk b ? Convert.ToBase64String(b.Value) : r.RawValue.ToString())).ToArray();

	[Theory]
	[InlineData(ACadVersion.AC1018, 0)]
	[InlineData(ACadVersion.AC1018, 1)]
	[InlineData(ACadVersion.AC1018, 2)]
	[InlineData(ACadVersion.AC1032, 0)]
	[InlineData(ACadVersion.AC1032, 1)]
	[InlineData(ACadVersion.AC1032, 2)]
	public void LayerNamesAndRawHandlesSurviveTwoGenerationsWithoutMutatingSource(ACadVersion version, int format)
	{
		var doc = document(version); var first = doc.Entities.First(); var second = doc.Entities.Last();
		first.ExtendedData.Add(new AppId(App), new ExtendedData(new ExtendedDataRecord[] {
			new ExtendedDataString("한글"), ExtendedDataControlString.Open, new ExtendedDataLayer(doc.Layers["한글_LAYER"].Handle),
			new ExtendedDataHandle(doc.Layers["한글_LAYER"].Handle), new ExtendedDataLayer(doc.Layers["0"].Handle),
			new ExtendedDataHandle(first.Handle), new ExtendedDataHandle(second.Handle), new ExtendedDataHandle(0),
			new ExtendedDataHandle(Missing), new ExtendedDataHandle(Missing), new ExtendedDataBinaryChunk(new byte[] { 0, 128, 255 }), ExtendedDataControlString.Close }));
		var expected = values(first); var ownerHandle = first.Handle; var appHandle = doc.AppIds[App].Handle;
		for (int generation = 0; generation < 2; generation++)
		{
			var records = first.ExtendedData.Get(App).Records.ToArray(); var original = values(first);
			var bytes = write(doc, format);
			Assert.Equal(original, values(first)); Assert.Equal(records, first.ExtendedData.Get(App).Records);
			if (format == 1)
			{
				var encoding = version < ACadVersion.AC1021 ? Encoding.GetEncoding(949) : Encoding.UTF8;
				var lines = encoding.GetString(bytes).Replace("\r\n", "\n").Split('\n');
				Assert.Contains(Enumerable.Range(0, lines.Length / 2), i => lines[2 * i].Trim() == "1003" && lines[2 * i + 1] == "한글_LAYER");
				Assert.Contains(Enumerable.Range(0, lines.Length / 2), i => lines[2 * i].Trim() == "1005" && lines[2 * i + 1].Trim() == Missing.ToString("X"));
			}
			doc = read(bytes, format); first = doc.GetCadObject<Entity>(ownerHandle);
			Assert.Equal(expected, values(first)); Assert.Equal(appHandle, doc.AppIds[App].Handle);
			Assert.Same(doc.AppIds[App], first.ExtendedData.Single().Key);
			foreach (var layer in first.ExtendedData.Get(App).Records.OfType<ExtendedDataLayer>())
				Assert.Same(doc.GetCadObject(layer.Value), layer.ResolveReference(doc));
			Assert.Null(doc.GetCadObject(Missing));
		}
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)]
	[InlineData(ACadVersion.AC1032)]
	public void IndependentlyWrittenDxfReferencesRetainTheirCodesAndUnresolvedHandleThroughDwg(ACadVersion version)
	{
		var doc = document(version); var entity = doc.Entities.First();
		entity.ExtendedData.Add(new AppId(App), new ExtendedData(new ExtendedDataRecord[] { new ExtendedDataString("SEED") }));
		var encoding = version < ACadVersion.AC1021 ? Encoding.GetEncoding(949) : Encoding.UTF8;
		var lines = encoding.GetString(write(doc, 1)).Replace("\r\n", "\n").Split('\n').ToList();
		var start = Enumerable.Range(0, lines.Count / 2).Single(i => lines[2 * i].Trim() == "1001" && lines[2 * i + 1] == App) * 2 + 2;
		Assert.Equal("1000", lines[start].Trim()); Assert.Equal("SEED", lines[start + 1]);
		// Raw DXF code/value pairs are independent of both reference writer branches.
		lines.RemoveRange(start, 2);
		lines.InsertRange(start, new[] { "1003", "한글_LAYER", "1005", Missing.ToString("X"), "1003", "0", "1005", entity.Handle.ToString("X") });
		doc = read(encoding.GetBytes(string.Join("\n", lines)), 1);
		for (int generation = 0; generation < 3; generation++)
		{
			var records = doc.GetCadObject<Entity>(entity.Handle).ExtendedData.Get(App).Records;
			Assert.Equal(new[] { 1003, 1005, 1003, 1005 }, records.Select(r => (int)r.Code));
			Assert.Same(doc.Layers["한글_LAYER"], Assert.IsType<ExtendedDataLayer>(records[0]).ResolveReference(doc));
			Assert.Equal(Missing, Assert.IsType<ExtendedDataHandle>(records[1]).Value);
			Assert.Same(doc.Layers["0"], Assert.IsType<ExtendedDataLayer>(records[2]).ResolveReference(doc));
			Assert.Equal(entity.Handle, Assert.IsType<ExtendedDataHandle>(records[3]).Value);
			if (generation < 2) doc = read(write(doc, 0), 0);
		}
	}

	[Theory]
	[InlineData(ACadVersion.AC1018, 0)]
	[InlineData(ACadVersion.AC1018, 1)]
	[InlineData(ACadVersion.AC1018, 2)]
	[InlineData(ACadVersion.AC1032, 0)]
	[InlineData(ACadVersion.AC1032, 1)]
	[InlineData(ACadVersion.AC1032, 2)]
	public void InvalidLayerReferenceIsRejectedWithoutChangingRecords(ACadVersion version, int format)
	{
		foreach (var kind in new[] { "zero", "missing", "wrong-type" })
		{
			var doc = document(version); var entity = doc.Entities.First();
			ulong handle = kind == "zero" ? 0 : kind == "missing" ? Missing : entity.Handle;
			entity.ExtendedData.Add(new AppId(App), new ExtendedData(new ExtendedDataRecord[] { new ExtendedDataString("KEEP"), new ExtendedDataLayer(handle), new ExtendedDataHandle(Missing) }));
			var before = values(entity); var records = entity.ExtendedData.Get(App).Records.ToArray();
			var exception = Assert.Throws<InvalidDataException>(() => write(doc, format));
			Assert.Contains("layer", exception.Message, StringComparison.OrdinalIgnoreCase);
			Assert.Equal(before, values(entity)); Assert.Equal(records, entity.ExtendedData.Get(App).Records);
		}
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)] [InlineData(ACadVersion.AC1032)]
	public void HandlelessR12LayerNameBindsAfterRegistrationAndSurvivesSupportedDwgVersions(ACadVersion version)
	{
		// Independent R12 text input: layer and APPID records deliberately have no group 5 handles.
		var raw = string.Join("\n", new[] {
			"0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1009", "0", "ENDSEC",
			"0", "SECTION", "2", "TABLES", "0", "TABLE", "2", "LAYER", "70", "1",
			"0", "LAYER", "2", "XDATA_ONLY", "70", "0", "62", "7", "6", "CONTINUOUS", "0", "ENDTAB",
			"0", "TABLE", "2", "APPID", "70", "1", "0", "APPID", "2", App, "70", "0", "0", "ENDTAB", "0", "ENDSEC",
			"0", "SECTION", "2", "ENTITIES", "0", "LINE", "5", "20", "8", "0",
			"10", "0", "20", "0", "30", "0", "11", "1", "21", "0", "31", "0", "1001", App,
			"1000", "BEFORE", "1003", "XDATA_ONLY", "1000", "AFTER", "0", "ENDSEC", "0", "EOF", "" });
		var doc = read(Encoding.ASCII.GetBytes(raw), 1);
		Assert.Equal(ACadVersion.AC1009, doc.Header.Version);
		Check(doc); doc.Header.Version = version; doc.CreateDefaults();
		for (int generation = 0; generation < 2; generation++) { doc = read(write(doc, 0), 0); Check(doc); }
		void Check(CadDocument drawing)
		{
			var records = drawing.Entities.Single().ExtendedData.Get(App).Records;
			Assert.Equal(3, records.Count); Assert.Equal("BEFORE", records[0].RawValue); Assert.Equal("AFTER", records[2].RawValue);
			var layer = Assert.IsType<ExtendedDataLayer>(records[1]); Assert.NotEqual(0UL, layer.Value);
			Assert.Same(drawing.Layers["XDATA_ONLY"], layer.ResolveReference(drawing));
		}
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)] [InlineData(ACadVersion.AC1032)]
	public void MissingRawDxfLayerNameReportsAnError(ACadVersion version)
	{
		var doc = document(version); var entity = doc.Entities.First();
		entity.ExtendedData.Add(App, new ExtendedData(new ExtendedDataRecord[] { new ExtendedDataLayer(doc.Layers["0"].Handle) }));
		var lines = Encoding.UTF8.GetString(write(doc, 1)).Replace("\r\n", "\n").Split('\n');
		var index = Enumerable.Range(0, lines.Length / 2).Single(i => lines[2 * i].Trim() == "1003") * 2;
		lines[index + 1] = "MISSING_XDATA_LAYER";
		var messages = new List<NotificationEventArgs>(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
		DxfReader.Read(input, (_, e) => messages.Add(e));
		Assert.Contains(messages, e => e.NotificationType == NotificationType.Error && e.Message.Contains("[XData]") && e.Message.Contains("MISSING_XDATA_LAYER") && e.Exception is InvalidDataException);
	}
}
