using System;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

public class OfficeRoundtripTests
{
    [Theory]
    [InlineData(ACadVersion.AC1014)]
    [InlineData(ACadVersion.AC1015)]
    [InlineData(ACadVersion.AC1018)]
    [InlineData(ACadVersion.AC1024)]
    [InlineData(ACadVersion.AC1027)]
    [InlineData(ACadVersion.AC1032)]
    public void ModelAndPaperLayoutLimitsKeepTheirOwnCoordinates(ACadVersion version)
    {
        var source = new CadDocument(); source.Header.Version = version;
        var index = 0;
        foreach (var layout in source.Layouts)
        {
            layout.MinLimits = new CSMath.XY(-13.125 - index, 7.75 + index);
            layout.MaxLimits = new CSMath.XY(812.5 + index, -65.375 - index);
            index++;
        }
        Assert.Contains(source.Layouts, l => l.IsPaperSpace);
        Assert.Contains(source.Layouts, l => !l.IsPaperSpace);
        var expected = source.Layouts.ToDictionary(l => l.Handle, l => (l.Name, l.MinLimits, l.MaxLimits));
        var current = source;
        for (var generation = 0; generation < 2; generation++)
        {
            current = Copy(current, "dwg");
            Assert.Equal(expected.OrderBy(p => p.Key), current.Layouts.ToDictionary(l => l.Handle, l => (l.Name, l.MinLimits, l.MaxLimits)).OrderBy(p => p.Key));
        }
        Assert.Equal(expected.OrderBy(p => p.Key), source.Layouts.ToDictionary(l => l.Handle, l => (l.Name, l.MinLimits, l.MaxLimits)).OrderBy(p => p.Key));
    }

    private static CadDocument Copy(CadDocument doc, string format)
    {
        using var output = new MemoryStream();
        if (format == "dwg") DwgWriter.Write(output, doc);
        else DxfWriter.Write(output, doc, binary: format == "binary");
        using var input = new MemoryStream(output.ToArray());
        return format == "dwg" ? DwgReader.Read(input) : DxfReader.Read(input);
    }

    // Synthetic only. Original AC1032 Version 4 layout is checked separately by the host project.
    [Theory]
    [InlineData(ACadVersion.AC1018)]
    [InlineData(ACadVersion.AC1024)]
    [InlineData(ACadVersion.AC1027)]
    [InlineData(ACadVersion.AC1032)]
    public void BlockReferenceContextDefaultAndReferencesSurviveTwoGenerations(ACadVersion version)
    {
        foreach (var contextVersion in new short[] { 3, 4 })
        foreach (var isDefault in new[] { false, true })
        foreach (var unrelatedFlag in new[] { false, true })
        {
            var doc = new CadDocument(); doc.Header.Version = version;
            var owner = new CadDictionary("CONTEXT_OWNER"); doc.RootDictionary.Add(owner);
            var first = new XRecord("FIRST"); var last = new XRecord("LAST");
            doc.RootDictionary.Add(first); doc.RootDictionary.Add(last);
            var scale = new Scale("CUSTOM") { PaperUnits = 1.25, DrawingUnits = 17.5 }; doc.Scales.Add(scale);
            var context = new BlockReferenceObjectContextData
            {
                Name = "CONTEXT", Version = contextVersion, Default = isDefault, HasFileToExtensionDictionary = unrelatedFlag,
                Rotation = 0.731, InsertionPoint = new(12.5, -23.75, 0.125),
                XScale = -2.5, YScale = 1, ZScale = 0.25, Scale = scale
            };
            owner.Add("CONTEXT", context); context.AddReactor(first); context.AddReactor(last);
            var handle = context.Handle; var ownerHandle = owner.Handle; var scaleHandle = scale.Handle;
            var reactors = context.Reactors.Select(r => r.Handle).ToArray();
            for (var generation = 0; generation < 2; generation++)
            {
                using var bytes = new MemoryStream();
                var notes = new System.Collections.Generic.List<string>();
                DwgWriter.Write(bytes, doc, notification: (_, e) => notes.Add(e.Message + " " + e.Exception));
                using var input = new MemoryStream(bytes.ToArray());
                doc = DwgReader.Read(input, new DwgReaderConfiguration { Failsafe = false }, (_, e) => notes.Add(e.Message + " " + e.Exception));
                Assert.True(doc.GetCadObject(handle) != null, "handle=" + handle + " " + string.Join(" | ", notes));
                context = Assert.IsType<BlockReferenceObjectContextData>(doc.GetCadObject(handle));
                Assert.Equal(contextVersion, context.Version); Assert.Equal(isDefault, context.Default);
                Assert.Equal(0.731, context.Rotation); Assert.Equal(new(12.5, -23.75, 0.125), context.InsertionPoint);
                Assert.Equal(-2.5, context.XScale); Assert.Equal(1, context.YScale); Assert.Equal(0.25, context.ZScale);
                Assert.Equal(ownerHandle, context.Owner.Handle); Assert.Equal(reactors, context.Reactors.Select(r => r.Handle));
                Assert.Equal(scaleHandle, context.Scale.Handle); Assert.Same(doc.Scales["CUSTOM"], context.Scale);
                Assert.Equal(1.25, context.Scale.PaperUnits); Assert.Equal(17.5, context.Scale.DrawingUnits);
                Assert.Same(context, ((CadDictionary)context.Owner).GetEntry<BlockReferenceObjectContextData>("CONTEXT"));
            }
        }
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
