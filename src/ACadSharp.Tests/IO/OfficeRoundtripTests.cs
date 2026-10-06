using System;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

public class OfficeRoundtripTests
{
    private static CadDocument Copy(CadDocument doc, string format)
    {
        using var output = new MemoryStream();
        if (format == "dwg") DwgWriter.Write(output, doc);
        else DxfWriter.Write(output, doc, binary: format == "binary");
        using var input = new MemoryStream(output.ToArray());
        return format == "dwg" ? DwgReader.Read(input) : DxfReader.Read(input);
    }

    [Theory]
    [InlineData(ACadVersion.AC1015, "dwg")]
    [InlineData(ACadVersion.AC1018, "dwg")]
    [InlineData(ACadVersion.AC1032, "dwg")]
    [InlineData(ACadVersion.AC1032, "ascii")]
    [InlineData(ACadVersion.AC1032, "binary")]
    public void PreviewBytesAndBlockGeometrySurviveTwoGenerations(ACadVersion version, string format)
    {
        var doc = new CadDocument(); doc.Header.Version = version;
        doc.ModelSpace.Preview = new byte[] { 1, 4, 7 };
        doc.PaperSpace.Preview = Enumerable.Range(0, 130).Select(i => (byte)i).ToArray();
        doc.BlockRecords.Add(new BlockRecord("NULL_PREVIEW") { Preview = null });
        foreach (var length in new[] { 0, 1, 126, 127, 128, 254, 255, 256, 513 })
        {
            var block = new BlockRecord("PREVIEW_" + length) { Preview = Enumerable.Range(0, length).Select(i => (byte)(i * 37)).ToArray() };
            block.Entities.Add(new Line(new(1, 2, 0), new(4, 6, 0)));
            doc.BlockRecords.Add(block);
        }
        for (var generation = 0; generation < 2; generation++)
        {
            doc = Copy(doc, format);
            Assert.Equal(new byte[] { 1, 4, 7 }, doc.ModelSpace.Preview);
            Assert.Equal(Enumerable.Range(0, 130).Select(i => (byte)i), doc.PaperSpace.Preview);
            Assert.Empty(doc.BlockRecords["NULL_PREVIEW"].Preview ?? Array.Empty<byte>());
            foreach (var length in new[] { 0, 1, 126, 127, 128, 254, 255, 256, 513 })
            {
                var block = doc.BlockRecords["PREVIEW_" + length];
                Assert.Equal(Enumerable.Range(0, length).Select(i => (byte)(i * 37)), block.Preview ?? Array.Empty<byte>());
                var line = Assert.IsType<Line>(Assert.Single(block.Entities));
                Assert.Equal(1, line.StartPoint.X); Assert.Equal(6, line.EndPoint.Y);
            }
        }
    }

    [Theory]
    [InlineData("dwg")]
    [InlineData("ascii")]
    [InlineData("binary")]
    public void MTextCountsFollowSerializedHeightsButKeepStaticAndAutomaticCounts(string format)
    {
        var doc = new CadDocument(); doc.Header.Version = ACadVersion.AC1032;
        foreach (var kind in new[] { ColumnType.DynamicColumns, ColumnType.StaticColumns })
        foreach (var auto in new[] { false, true })
        {
            var text = new MText { Value = kind + ":" + auto, Style = doc.TextStyles["Standard"], LineSpacingStyle = LineSpacingStyleType.AtLeast };
            text.ColumnData.ColumnType = kind; text.ColumnData.AutoHeight = auto;
            text.ColumnData.ColumnCount = 7; text.ColumnData.Width = 30; text.ColumnData.Gutter = 3;
            if (kind == ColumnType.DynamicColumns && !auto) text.ColumnData.Heights.AddRange(new[] { 12.5, 18.75 });
            doc.Entities.Add(text);
        }
        for (var generation = 0; generation < 2; generation++)
        {
            doc = Copy(doc, format);
            foreach (var text in doc.Entities.OfType<MText>())
            {
                var manual = text.ColumnData.ColumnType == ColumnType.DynamicColumns && !text.ColumnData.AutoHeight;
                Assert.Equal(manual ? 2 : 7, text.ColumnData.ColumnCount);
                Assert.Equal(manual ? new[] { 12.5, 18.75 } : Array.Empty<double>(), text.ColumnData.Heights);
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ModernDimensionFitDoesNotBecomeConstantThree(int fit)
    {
        var doc = new CadDocument(); doc.Header.Version = ACadVersion.AC1032;
        doc.DimensionStyles["Standard"].DimensionTextArrowFit = (TextArrowFitType)fit;
        for (var generation = 0; generation < 2; generation++)
        {
            doc = Copy(doc, "dwg");
            Assert.Equal(fit, (int)doc.DimensionStyles["Standard"].DimensionTextArrowFit);
            Assert.Equal(0, doc.DimensionStyles["Standard"].DimensionFit); // DIMFIT is not the modern DIMATFIT field.
        }
    }
}
