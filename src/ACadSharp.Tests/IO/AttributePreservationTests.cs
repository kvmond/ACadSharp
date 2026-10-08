using System;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

// Synthetic inputs only: these regressions are also run by Air CAD's .NET 10 runner.
public class AttributePreservationTests
{
    [Theory]
    [InlineData(ACadVersion.AC1018, "ascii")]
    [InlineData(ACadVersion.AC1018, "binary")]
    [InlineData(ACadVersion.AC1032, "ascii")]
    [InlineData(ACadVersion.AC1032, "binary")]
    [InlineData(ACadVersion.AC1018, "dwg")]
    [InlineData(ACadVersion.AC1032, "dwg")]
    public void TextAndAttributesKeepMirrorThicknessAndStyleAcrossTwoGenerations(ACadVersion version, string format)
    {
        var source = new CadDocument(version);
        source.TextStyles.Add(new TextStyle("OUTER"));
        var block = new BlockRecord("ATTRIBUTES"); source.BlockRecords.Add(block);
        foreach (var mirror in new[] { 0, 2, 4, 6 })
        {
            var definition = new AttributeDefinition { Tag = "A" + mirror, Value = "Value" + mirror,
                Mirror = (TextMirrorFlag)mirror, Height = 2, Thickness = mirror == 0 ? -2.5 : mirror,
                Style = source.TextStyles["OUTER"] };
            block.Entities.Add(definition);
            source.Entities.Add(new TextEntity { Value = "T" + mirror, Mirror = definition.Mirror,
                Thickness = definition.Thickness, Height = 2, Style = definition.Style });
        }
        source.Entities.Add(new Insert(block));
        var expected = Texts(source).Select(Scalars).ToArray();
        var current = source;
        for (var generation = 0; generation < 2; generation++)
        {
            current = Copy(current, format);
            Assert.Equal(expected, Texts(current).Select(Scalars));
            Assert.All(Texts(current), t => Assert.Same(current.TextStyles["OUTER"], t.Style));
            Assert.All(Texts(current).OfType<AttributeBase>(), a => Assert.Equal(AttributeType.SingleLine, a.AttributeType));
            // A DXF input must also survive the app's subsequent DWG generations.
            var dwg = current;
            for (var saved = 0; saved < 2; saved++)
            {
                dwg = Copy(dwg, "dwg");
                Assert.Equal(expected, Texts(dwg).Select(Scalars));
            }
        }
        Assert.Equal(expected, Texts(source).Select(Scalars));
    }

    [Theory]
    [InlineData("definition-clone")]
    [InlineData("attribute-clone")]
    [InlineData("definition-to-attribute")]
    [InlineData("attribute-to-definition")]
    public void AttributeCopiesOwnTheirEmbeddedTextColumnsAndTargetTableReferences(string operation)
    {
        var source = new CadDocument(ACadVersion.AC1032);
        var original = Multiline(operation.StartsWith("attribute-"));
        source.Entities.Add(original);
        var text = original.MText; var heights = text.ColumnData.Heights.ToArray();
        var originalStyle = text.Style; var seed = source.Header.HandleSeed;
        var handle = original.Handle; var value = text.Value;
        AttributeBase copy = operation switch
        {
            "definition-clone" => (AttributeBase)original.Clone(),
            "attribute-clone" => (AttributeBase)original.Clone(),
            "definition-to-attribute" => new AttributeEntity((AttributeDefinition)original),
            _ => new AttributeDefinition((AttributeEntity)original),
        };
        Assert.NotNull(copy.MText);
        Assert.NotSame(text, copy.MText); Assert.NotSame(text.ColumnData, copy.MText.ColumnData);
        Assert.NotSame(text.ColumnData.Heights, copy.MText.ColumnData.Heights);
        Assert.Equal(heights, copy.MText.ColumnData.Heights);
        Assert.Null(copy.Document); Assert.Null(copy.MText.Document); Assert.Equal(0UL, copy.MText.Handle);
        var target = new CadDocument(ACadVersion.AC1032);
        target.TextStyles.Add(new TextStyle("INNER")); target.Layers.Add(new Layer("INNER_LAYER"));
        target.LineTypes.Add(new LineType("INNER_LINE"));
        target.Entities.Add(copy);
        Assert.Same(target.TextStyles["INNER"], copy.MText.Style);
        Assert.Same(target.Layers["INNER_LAYER"], copy.MText.Layer);
        Assert.Same(target.LineTypes["INNER_LINE"], copy.MText.LineType);
        // Embedded MTEXT has no independent object-map entry or generated handle.
        Assert.Equal(0UL, copy.MText.Handle); Assert.Null(target.GetCadObject(0));
        Assert.Same(source, original.Document); Assert.Same(source, text.Document);
        Assert.Same(originalStyle, text.Style); Assert.Equal(seed, source.Header.HandleSeed);
        Assert.Equal(handle, original.Handle); Assert.Same(original, source.GetCadObject(handle));
        copy.MText.Value = "CHANGED"; copy.MText.ColumnData.Heights[0] = 999;
        copy.MText.ColumnData.Width = 888; copy.MText.Style = new TextStyle("REPLACEMENT");
        Assert.Equal(value, text.Value); Assert.Equal(heights, text.ColumnData.Heights);
        Assert.Equal(20, text.ColumnData.Width); Assert.Same(originalStyle, text.Style);
        target.Entities.Remove(copy);
        Assert.Null(copy.MText.Document); Assert.Null(copy.MText.Style.Document);
        Assert.Same(source, text.Document); Assert.Same(originalStyle, text.Style);
    }

    [Fact]
    public void PlainMTextCloneDoesNotShareColumnHeights()
    {
        var original = Multiline(false).MText;
        var copy = (MText)original.Clone();
        Assert.NotSame(original.ColumnData, copy.ColumnData);
        Assert.NotSame(original.ColumnData.Heights, copy.ColumnData.Heights);
        copy.ColumnData.Heights.Clear(); copy.ColumnData.Gutter = 100;
        Assert.Equal(new[] { 12.5, 18.75 }, original.ColumnData.Heights);
        Assert.Equal(3, original.ColumnData.Gutter);
    }

    [Theory]
    [InlineData("ascii", AttributeType.MultiLine)]
    [InlineData("binary", AttributeType.MultiLine)]
    [InlineData("dwg", AttributeType.MultiLine)]
    [InlineData("ascii", AttributeType.ConstantMultiLine)]
    [InlineData("binary", AttributeType.ConstantMultiLine)]
    [InlineData("dwg", AttributeType.ConstantMultiLine)]
    public void ModernMultilineTypeDoesNotOverwriteTextMirrorOrColumnData(string format, AttributeType type)
    {
        var source = new CadDocument(ACadVersion.AC1032);
        var block = new BlockRecord("MULTI"); source.BlockRecords.Add(block);
        var definition = (AttributeDefinition)Multiline(false); definition.AttributeType = type; block.Entities.Add(definition);
        var insert = new Insert(block); source.Entities.Add(insert);
        var expected = Scalars(definition); var originalText = definition.MText;
        for (var generation = 0; generation < 2; generation++)
        {
            source = Copy(source, format);
            foreach (var attribute in Texts(source).OfType<AttributeBase>())
            {
                Assert.Equal(expected, Scalars(attribute));
                Assert.Equal(type, attribute.AttributeType);
                Assert.Equal("FIRST\\PSECOND", attribute.MText.Value);
                Assert.Equal(new[] { 12.5, 18.75 }, attribute.MText.ColumnData.Heights);
                Assert.Same(source.TextStyles["INNER"], attribute.MText.Style);
                Assert.Equal(AttachmentPointType.BottomRight, attribute.MText.AttachmentPoint);
            }
        }
        Assert.Equal("FIRST\\PSECOND", originalText.Value);
        Assert.Equal(new[] { 12.5, 18.75 }, originalText.ColumnData.Heights);
    }

    [Theory]
    [InlineData(ACadVersion.AC1018, "dwg")]
    [InlineData(ACadVersion.AC1027, "dwg")]
    [InlineData(ACadVersion.AC1018, "ascii")]
    [InlineData(ACadVersion.AC1018, "binary")]
    public void LegacyMultilineRemainsAnExplicitUnsupportedStorageBoundary(ACadVersion version, string format)
    {
        // This patch does not implement the old-format multiline representation or enable product editing.
        var source = new CadDocument(version); var definition = Multiline(false); source.Entities.Add(definition);
        var copy = Copy(source, format);
        var attribute = Assert.IsType<AttributeDefinition>(Assert.Single(copy.Entities));
        Assert.Equal(AttributeType.SingleLine, attribute.AttributeType); Assert.Null(attribute.MText);
        Assert.Equal(TextMirrorFlag.Backward | TextMirrorFlag.UpsideDown, attribute.Mirror);
        Assert.Equal(AttributeType.MultiLine, definition.AttributeType); Assert.NotNull(definition.MText);
    }

    [Theory]
    [InlineData(ACadVersion.AC1009, false)]
    [InlineData(ACadVersion.AC1018, false)]
    [InlineData(ACadVersion.AC1018, true)]
    [InlineData(ACadVersion.AC1032, false)]
    public void IndependentDxfTextFlagsAreReadInTheirOwnSubclass(ACadVersion version, bool legacyAttributeSubclass)
    {
        // Hand-authored pairs, not bytes produced by the writer under test.
        var markers = version == ACadVersion.AC1009 ? "" : "100\nAcDbEntity\n8\n0\n100\nAcDbText\n";
        var flags = "71\n6\n";
        var attribute = version == ACadVersion.AC1009 ? "" : "100\nAcDbAttributeDefinition\n";
        var dxf = "0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\n" + version + "\n0\nENDSEC\n"
            + "0\nSECTION\n2\nENTITIES\n0\nATTDEF\n5\nA1\n" + markers
            + "10\n1\n20\n2\n30\n0\n40\n2\n1\nTEXT\n39\n-3.25\n"
            + (legacyAttributeSubclass ? attribute + flags : flags + attribute)
            + "2\nTAG\n70\n0\n3\nPrompt\n0\nENDSEC\n0\nEOF\n";
        using var bytes = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(dxf));
        var doc = DxfReader.Read(bytes);
        var definition = Assert.IsType<AttributeDefinition>(Assert.Single(doc.Entities));
        Assert.Equal(TextMirrorFlag.Backward | TextMirrorFlag.UpsideDown, definition.Mirror);
        Assert.Equal(AttributeType.SingleLine, definition.AttributeType);
        Assert.Equal(-3.25, definition.Thickness);
    }

    [Fact]
    public void EmbeddedReplacementDetachesOldTablesAndCannotStealRegisteredSourceText()
    {
        var source = new CadDocument(ACadVersion.AC1032);
        var original = Multiline(false).MText; source.Entities.Add(original);
        var sourceHandle = original.Handle; var sourceStyle = original.Style; var sourceSeed = source.Header.HandleSeed;
        var target = new CadDocument(ACadVersion.AC1032);
        var definition = Multiline(false); target.Entities.Add(definition);
        var retired = definition.MText;
        definition.MText = original;
        Assert.NotSame(original, definition.MText);
        Assert.Null(retired.Document); Assert.Null(retired.Style.Document);
        Assert.Same(target, definition.MText.Document); Assert.Equal(0UL, definition.MText.Handle);
        Assert.Same(target.TextStyles["INNER"], definition.MText.Style);
        Assert.Same(source, original.Document); Assert.Same(sourceStyle, original.Style);
        Assert.Equal(sourceSeed, source.Header.HandleSeed); Assert.Same(original, source.GetCadObject(sourceHandle));
        var embedded = definition.MText;
        definition.MText = embedded; // Idempotent assignment does not detach a live child.
        Assert.Same(embedded, definition.MText); Assert.Same(target, embedded.Document);
        target.TextStyles.Remove("INNER");
        Assert.Same(target.TextStyles["Standard"], embedded.Style);
        Assert.Same(sourceStyle, original.Style); Assert.Equal("INNER", original.Style.Name);
        definition.MText = null;
        Assert.Null(embedded.Document); Assert.Null(embedded.Style.Document);
        Assert.Null(target.GetCadObject(0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedDetachedTextBecomesIndependentWhenRegisteredOrReplaced(bool replaceBeforeRegistering)
    {
        var child = Multiline(false).MText;
        var first = new AttributeDefinition { Tag = "FIRST", MText = child, AttributeType = AttributeType.MultiLine };
        var second = new AttributeDefinition { Tag = "SECOND", MText = child, AttributeType = AttributeType.MultiLine };
        var source = new CadDocument(ACadVersion.AC1032); source.Entities.Add(first);
        var style = child.Style; var seed = source.Header.HandleSeed;
        if (replaceBeforeRegistering)
        {
            second.MText = new MText { Value = "SECOND" };
            Assert.Same(source, child.Document); Assert.Same(style, child.Style);
        }
        var target = new CadDocument(ACadVersion.AC1032); target.Entities.Add(second);
        Assert.NotSame(first.MText, second.MText); Assert.Same(source, child.Document);
        Assert.Equal(seed, source.Header.HandleSeed); Assert.Same(style, child.Style);
        second.MText.ColumnData.Heights.Clear();
        Assert.Equal(new[] { 12.5, 18.75 }, child.ColumnData.Heights);
        target.Entities.Remove(second);
        Assert.Same(source, child.Document); Assert.Same(style, child.Style);
        Assert.Same(source.TextStyles["INNER"], child.Style);
    }

    private static AttributeBase Multiline(bool reference)
    {
        AttributeBase result = reference ? new AttributeEntity() : new AttributeDefinition();
        result.Tag = "NOTE"; result.Value = "FALLBACK"; result.Height = 2; result.Thickness = -1.5;
        result.Mirror = TextMirrorFlag.Backward | TextMirrorFlag.UpsideDown;
        result.AttributeType = AttributeType.MultiLine;
        result.MText = new MText { Value = "FIRST\\PSECOND", Height = 2,
            Style = new TextStyle("INNER"), Layer = new Layer("INNER_LAYER"), LineType = new LineType("INNER_LINE"),
            AttachmentPoint = AttachmentPointType.BottomRight, LineSpacingStyle = LineSpacingStyleType.AtLeast };
        var columns = result.MText.ColumnData;
        columns.ColumnType = ColumnType.DynamicColumns; columns.ColumnCount = 2;
        columns.Width = 20; columns.Gutter = 3; columns.Heights.AddRange(new[] { 12.5, 18.75 });
        return result;
    }

    private static string Scalars(TextEntity text) => string.Join("|", text.Value, (short)text.Mirror, text.Thickness, text.Height, text.Style.Name);
    private static TextEntity[] Texts(CadDocument doc) => doc.BlockRecords.SelectMany(b => b.Entities)
        .OfType<TextEntity>().Concat(doc.Entities.OfType<Insert>().SelectMany(i => i.Attributes)).OrderBy(t => t.Value).ThenBy(t => t.ObjectName).ToArray();
    private static CadDocument Copy(CadDocument doc, string format)
    {
        using var output = new MemoryStream();
        if (format == "dwg") DwgWriter.Write(output, doc); else DxfWriter.Write(output, doc, binary: format == "binary");
        using var input = new MemoryStream(output.ToArray());
        return format == "dwg" ? DwgReader.Read(input) : DxfReader.Read(input);
    }
}
