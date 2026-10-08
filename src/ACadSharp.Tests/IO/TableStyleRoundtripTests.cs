using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

// Public fork fixtures are synthetic. AutoCAD source records are checked by the private host tests.
public class TableStyleRoundtripTests
{
    public static TableStyle.CellStyle[] Cells(TableStyle s) => new[] { s.TableCellStyle, s.TitleCellStyle, s.HeaderCellStyle, s.DataCellStyle }.Concat(s.CellStyles).ToArray();
    public static TableStyle.CellBorder[] Borders(TableStyle.CellStyle c) => new[] { c.TopBorder, c.RightBorder, c.BottomBorder, c.LeftBorder, c.VerticalInsideBorder, c.HorizontalInsideBorder };
    public static string Snapshot(TableStyle s) => s.Name + "|" + s.Description + "|" + string.Join(",", new[] { "RawHeaderByte", "RawHeaderValue1", "RawHeaderValue2", "RawHeaderHandle" }.Select(p => typeof(TableStyle).GetProperty(p, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(s))) + "|" + string.Join("\n", Cells(s).Select(CellSnapshot));
    private static string CellSnapshot(TableStyle.CellStyle c)
    {
        var text = c.TextStyle;
        return string.Join("|", new object[] { c.Name, typeof(TableStyle.CellStyle).GetProperty("Id", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!,
            c.StyleClass, c.Type, c.HasData, c.CellPropertyOverrideFlags, c.PropertyOverrideFlags, c.PropertyFlags, c.TableCellStylePropertyFlags, c.BackgroundColor, c.ContentLayoutFlags,
            c.ValueDataType, c.ValueUnitType, c.ValueFormatString ?? "", c.Rotation, c.Scale, c.Alignment, c.Color, text?.Name ?? "<null>", text?.Handle ?? 0, c.TextHeight,
            c.MarginOverrideFlags, c.VerticalMargin, c.HorizontalMargin, c.BottomMargin, c.RightMargin, c.MarginHorizontalSpacing, c.MarginVerticalSpacing,
            string.Join(";", Borders(c).Select(b => string.Join(",", new object[] { b.EdgeFlags, b.ApplyBorder, b.PropertyOverrideFlags, b.Type, b.Color, b.LineWeight,
                b.LineType?.Name ?? "<null>", b.LineType?.Handle ?? 0, b.IsInvisible, b.DoubleLineSpacing }))) }.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
    }
    private static CadDocument Copy(CadDocument source)
    {
        var before = source.TableStyles.ToDictionary(s => s.Name, Snapshot);
        using var output = new MemoryStream(); DwgWriter.Write(output, source);
        Assert.Equal(before.OrderBy(p => p.Key), source.TableStyles.ToDictionary(s => s.Name, Snapshot).OrderBy(p => p.Key));
        using var input = new MemoryStream(output.ToArray()); return DwgReader.Read(input, new DwgReaderConfiguration { Failsafe = false });
    }
    [Fact]
    public void NoneIsDistinctFromEveryAciAndRgbBlack()
    {
        Assert.True(Color.None.IsNone); Assert.False(Color.None.IsTrueColor); Assert.False(Color.None.IsByLayer); Assert.False(Color.None.IsByBlock);
        Assert.Equal(Color.None, Color.None); Assert.Equal(Color.None.GetHashCode(), Color.None.GetHashCode());
        Assert.NotEqual(Color.None, new Color(0, 0, 0)); Assert.NotEqual(Color.None, Color.ByEntity); Assert.Equal(257, Color.ByEntity.Index);
        Assert.Throws<InvalidOperationException>(() => Color.None.Index);
        Assert.Throws<InvalidOperationException>(() => Color.None.GetRgb().ToArray());
        Assert.Throws<InvalidOperationException>(() => Color.None.GetApproxIndex());
        for (short i = 0; i <= 257; i++) { var color = new Color(i); Assert.Equal(i, color.Index); Assert.False(color.IsNone); Assert.NotEqual(Color.None, color); }
        var rgb = new Color(17, 23, 199); Assert.True(rgb.IsTrueColor); Assert.Equal(new byte[] { 17, 23, 199 }, rgb.GetRgb().ToArray());
    }
    [Theory]
    [InlineData(ACadVersion.AC1027)]
    [InlineData(ACadVersion.AC1024)]
    [InlineData(ACadVersion.AC1032)]
    public void FullDefaultAndCustomCellsSurviveTwoGenerations(ACadVersion version)
    {
        var doc = new CadDocument(); doc.Header.Version = version;
        var style = new TableStyle("SYNTHETIC") { Description = "Custom cells \u03A9" }; doc.TableStyles.Add(style);
        typeof(TableStyle).GetProperty("RawHeaderByte", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(style, (byte)7);
        typeof(TableStyle).GetProperty("RawHeaderValue1", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(style, 8);
        typeof(TableStyle).GetProperty("RawHeaderValue2", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(style, 101);
        foreach (var id in new[] { 103, 107 })
        {
            var c = new TableStyle.CellStyle { Name = "CUSTOM_" + id, HasData = true };
            typeof(TableStyle.CellStyle).GetProperty("Id", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, id); style.CellStyles.Add(c);
            typeof(TableStyle.CellStyle).GetProperty("RawIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, id + 200);
        }
        int n = 0;
        foreach (var c in Cells(style))
        {
            n++; c.BackgroundColor = n % 2 == 0 ? Color.None : new Color(12, 23, 34); c.Color = n % 2 == 0 ? Color.ByEntity : Color.ByBlock;
            c.ValueDataType = 2; c.ValueUnitType = 1; c.ValueFormatString = "%lu2%pr3"; c.Alignment = n; c.Rotation = n * 0.125;
            c.TextHeight = 2.25 + n; c.Scale = 1.125; c.PropertyFlags = 1; c.PropertyOverrideFlags = (TableStyle.CellStylePropertyFlags)7;
            c.CellPropertyOverrideFlags = (TableStyle.CellStylePropertyFlags)3;
            c.TableCellStylePropertyFlags = (TableStyle.CellStylePropertyFlags)0x8000; c.ContentLayoutFlags = (TableStyle.CellContentLayoutFlags)1;
            c.MarginOverrideFlags = TableStyle.MarginFlags.Override; c.VerticalMargin = n * 0.125; c.HorizontalMargin = n * 0.25;
            c.BottomMargin = n * 0.5; c.RightMargin = n * 0.75; c.MarginHorizontalSpacing = 1.5; c.MarginVerticalSpacing = 2.5;
            // Table's zero STYLE handle is intentional; every nonzero reference must register immediately.
            if (c != style.TableCellStyle) c.TextStyle = new TextStyle("TEXT_" + n) { Filename = "txt.shx", Width = 0.875 };
            int b = 0;
            foreach (var border in Borders(c))
            {
                b++; border.ApplyBorder = true; border.PropertyOverrideFlags = (TableStyle.BorderPropertyFlags)31;
                border.Type = (TableStyle.BorderType)(b % 2); border.Color = b % 2 == 0 ? Color.None : new Color((short)b);
                border.LineWeight = LineWeightType.W25; border.IsInvisible = b % 2 == 0; border.DoubleLineSpacing = b * 0.125;
                border.LineType = new LineType("LINE_" + b);
            }
        }
        Assert.Null(style.TableCellStyle.TextStyle);
        var expected = Snapshot(style); var handle = style.Handle;
        var clone = (TableStyle)style.Clone(); clone.Name = "CLONE"; doc.TableStyles.Add(clone);
        Assert.Equal(expected.Replace("SYNTHETIC|", "CLONE|"), Snapshot(clone));
        clone.TitleCellStyle.CellPropertyOverrideFlags = (TableStyle.CellStylePropertyFlags)123;
        clone.CellStyles[0].TopBorder.DoubleLineSpacing = 9.25; Assert.Equal(expected, Snapshot(style));
        doc.TableStyles.Remove("CLONE");
        for (int generation = 0; generation < 2; generation++)
        {
            doc = Copy(doc); style = doc.TableStyles["SYNTHETIC"];
            Assert.Equal(handle, style.Handle); Assert.Equal(expected, Snapshot(style));
            Assert.Equal(new[] { "CUSTOM_103", "CUSTOM_107" }, style.CellStyles.Select(c => c.Name));
            Assert.Equal(new int?[] { 303, 307 }, style.CellStyles.Select(c => (int?)typeof(TableStyle.CellStyle).GetProperty("RawIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)));
            foreach (var c in Cells(style))
            {
                if (c.TextStyle != null) Assert.Same(c.TextStyle, doc.GetCadObject(c.TextStyle.Handle));
                foreach (var b in Borders(c)) Assert.Same(b.LineType, doc.GetCadObject(b.LineType.Handle));
            }
        }
    }
    [Theory]
    [InlineData(ACadVersion.AC1018)]
    [InlineData(ACadVersion.AC1032)]
    public void NoneBackgroundUsesCmcAndKeepsRowsRegistered(ACadVersion version)
    {
        var doc = new CadDocument(); doc.Header.Version = version;
        foreach (var c in Cells(doc.TableStyles[TableStyle.DefaultName])) c.BackgroundColor = Color.None;
        for (var i = 0; i < 2; i++)
        {
            doc = Copy(doc);
            foreach (var c in Cells(doc.TableStyles[TableStyle.DefaultName]).Skip(1))
            { Assert.True(c.BackgroundColor.IsNone); Assert.Same(doc.TextStyles[TextStyle.DefaultName], c.TextStyle); }
        }
    }
    [Fact]
    public void SharedReferencesRefreshByResourceAndRemovedChildrenStayDetached()
    {
        var doc = new CadDocument(); var first = new TableStyle("FIRST"); var other = new TableStyle("OTHER");
        doc.TableStyles.Add(first); doc.TableStyles.Add(other);
        var a = new TextStyle("A"); var b = new TextStyle("B"); var la = new LineType("LA"); var lb = new LineType("LB");
        first.TitleCellStyle.TextStyle = a; first.HeaderCellStyle.TextStyle = a; first.DataCellStyle.TextStyle = b;
        other.TitleCellStyle.TextStyle = a;
        first.TitleCellStyle.TopBorder.LineType = la; first.HeaderCellStyle.TopBorder.LineType = la; first.DataCellStyle.TopBorder.LineType = lb;
        var added = new TableStyle.CellStyle { Name = "ADDED", TextStyle = a }; added.TopBorder.LineType = la; first.CellStyles.Add(added);
        Assert.Single(doc.TextStyles.GetReferences("A"), r => r == first); Assert.Single(doc.TextStyles.GetReferences("B"), r => r == first);
        a.Name = "RENAMED"; first.CellStyles[0] = added; first.TitleCellStyle = first.TitleCellStyle;
        first.CellStyles.Remove(added); first.DataCellStyle.TextStyle = a; first.DataCellStyle.TopBorder.LineType = la;
        Assert.DoesNotContain(first, doc.TextStyles.GetReferences("B")); Assert.DoesNotContain(first, doc.LineTypes.GetReferences("LB"));
        doc.TextStyles.Remove("B"); doc.LineTypes.Remove("LB");
        Assert.Same(a, first.DataCellStyle.TextStyle); Assert.Same(la, first.DataCellStyle.TopBorder.LineType);
        doc.TextStyles.Remove("RENAMED"); doc.LineTypes.Remove("LA");
        Assert.Same(doc.TextStyles[TextStyle.DefaultName], first.TitleCellStyle.TextStyle);
        Assert.Same(first.TitleCellStyle.TextStyle, first.HeaderCellStyle.TextStyle); Assert.Same(first.TitleCellStyle.TextStyle, first.DataCellStyle.TextStyle);
        Assert.Same(first.TitleCellStyle.TextStyle, other.TitleCellStyle.TextStyle);
        Assert.Same(doc.LineTypes[LineType.ByLayerName], first.TitleCellStyle.TopBorder.LineType);
        Assert.Same(first.TitleCellStyle.TopBorder.LineType, first.HeaderCellStyle.TopBorder.LineType);
        Assert.Same(a, added.TextStyle); Assert.Same(la, added.TopBorder.LineType);
    }
    [Fact]
    public void ReferenceCallbacksAreKeyedByOwnerAndPermitReentrantRegistration()
    {
        var doc = new CadDocument(); var first = new ACadSharp.Entities.Line(); var second = new ACadSharp.Entities.Line();
        doc.ModelSpace.Entities.Add(first); doc.ModelSpace.Entities.Add(second);
        var a = new TextStyle("A"); var b = new TextStyle("B"); doc.TextStyles.Add(a); doc.TextStyles.Add(b);
        var update = doc.TextStyles.GetType().GetMethod("UpdateReference", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var calls = new List<string>(); var reenter = false;
        void Set(CadObject owner, TextStyle entry, Action<TextStyle> assign) => update.Invoke(doc.TextStyles, new object[] { owner, entry, assign });
        Set(first, a, _ => calls.Add("old"));
        Set(first, a, _ => { calls.Add("first-A"); if (reenter) Set(first, b, _ => calls.Add("reentered-B")); });
        Set(second, a, _ => calls.Add("second-A")); Set(first, b, _ => calls.Add("old-B"));
        a.Name = "RENAMED"; calls.Clear(); reenter = true; doc.TextStyles.Remove("RENAMED");
        Assert.Equal(new[] { "first-A", "reentered-B", "second-A" }, calls);
        Assert.Single(doc.TextStyles.GetReferences("B"), r => r == first);
        calls.Clear(); doc.TextStyles.Remove("B"); Assert.Equal(new[] { "reentered-B" }, calls);
    }
    [Fact]
    public void ChildrenCannotBeSharedBetweenOwnersAndCloneIsIndependentAcrossDocuments()
    {
        var first = new TableStyle("FIRST"); var other = new TableStyle("OTHER");
        Assert.Throws<ArgumentException>(() => other.DataCellStyle = first.DataCellStyle);
        Assert.Throws<ArgumentException>(() => other.CellStyles.Add(first.DataCellStyle));
        Assert.Throws<ArgumentException>(() => other.DataCellStyle.TopBorder = first.DataCellStyle.TopBorder);
        Assert.Throws<ArgumentNullException>(() => other.CellStyles.Add(null));
        var doc = new CadDocument(); doc.TableStyles.Add(first);
        first.DataCellStyle.TextStyle = new TextStyle("CUSTOM") { Filename = "simplex.shx" };
        first.DataCellStyle.TopBorder.LineType = new LineType("CUSTOM");
        first.CellStyles.Add(new TableStyle.CellStyle { Name = "A", TextStyle = first.DataCellStyle.TextStyle });
        var clone = (TableStyle)first.Clone(); var secondDoc = new CadDocument(); secondDoc.TableStyles.Add(clone);
        Assert.NotSame(first.DataCellStyle, clone.DataCellStyle); Assert.NotSame(first.DataCellStyle.TextStyle, clone.DataCellStyle.TextStyle);
        Assert.Same(secondDoc.TextStyles["CUSTOM"], clone.CellStyles[0].TextStyle);
        clone.DataCellStyle.TopBorder.DoubleLineSpacing = 4.25; Assert.Equal(0, first.DataCellStyle.TopBorder.DoubleLineSpacing);
        clone.CellStyles.Clear(); Assert.Single(first.CellStyles);
        doc.TableStyles.Remove("FIRST"); Assert.Null(first.Document); Assert.Null(first.DataCellStyle.TextStyle.Document);
        doc.TextStyles.Remove("CUSTOM"); Assert.Equal("CUSTOM", first.DataCellStyle.TextStyle.Name);
        doc.TableStyles.Add(first); Assert.Same(doc.TextStyles["CUSTOM"], first.DataCellStyle.TextStyle);
    }
    [Fact]
    public void RawHeaderSurvivesCloneAndUnresolvedOwnershipRefusesWrite()
    {
        var style = new TableStyle("RAW"); var property = typeof(TableStyle).GetProperty("RawHeaderValue2", BindingFlags.Instance | BindingFlags.NonPublic)!;
        property.SetValue(style, 101); var clone = (TableStyle)style.Clone(); Assert.Equal(101, property.GetValue(clone));
        var doc = new CadDocument(); doc.TableStyles.Add(style);
        typeof(TableStyle).GetProperty("RawHeaderHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(style, 123UL);
        var before = Snapshot(style); using var stream = new MemoryStream();
        Assert.Throws<NotSupportedException>(() => DwgWriter.Write(stream, doc)); Assert.Equal(before, Snapshot(style));
    }
    [Fact]
    public void ReplacingOwnedCellsAndBordersRemovesOldCallbacks()
    {
        var doc = new CadDocument(); var style = new TableStyle("REPLACE"); doc.TableStyles.Add(style);
        var old = style.TitleCellStyle; old.TextStyle = new TextStyle("OLD"); var oldBorder = old.TopBorder; oldBorder.LineType = new LineType("OLD");
        var replacement = TableStyle.CellStyle.DefaultTitleCellStyle; replacement.TextStyle = new TextStyle("NEW"); style.TitleCellStyle = replacement;
        Assert.DoesNotContain(style, doc.TextStyles.GetReferences("OLD")); Assert.DoesNotContain(style, doc.LineTypes.GetReferences("OLD"));
        doc.TextStyles.Remove("OLD"); doc.LineTypes.Remove("OLD"); Assert.Equal("OLD", old.TextStyle.Name); Assert.Equal("OLD", oldBorder.LineType.Name);
        style.TitleCellStyle.TopBorder.LineType = new LineType("REPLACED_BORDER"); var priorBorder = style.TitleCellStyle.TopBorder;
        style.TitleCellStyle.TopBorder = new TableStyle.CellBorder(TableStyle.CellEdgeFlags.Top) { LineType = new LineType("FINAL_BORDER") };
        doc.LineTypes.Remove("REPLACED_BORDER"); Assert.Equal("REPLACED_BORDER", priorBorder.LineType.Name);
        Assert.Equal("FINAL_BORDER", style.TitleCellStyle.TopBorder.LineType.Name);
    }
    [Theory]
    [InlineData("legacy-dwg")]
    [InlineData("ascii-dxf")]
    [InlineData("binary-dxf")]
    public void CustomCellsCannotBeSilentlyDowngraded(string format)
    {
        var doc = new CadDocument(); doc.TableStyles[TableStyle.DefaultName].CellStyles.Add(new TableStyle.CellStyle { Name = "CUSTOM", HasData = true });
        using var stream = new MemoryStream();
        if (format == "legacy-dwg") { doc.Header.Version = ACadVersion.AC1018; Assert.Throws<NotSupportedException>(() => DwgWriter.Write(stream, doc)); }
        else Assert.Throws<NotSupportedException>(() => DxfWriter.Write(stream, doc, binary: format == "binary-dxf"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyDxfRowsBindToBuiltinsRatherThanCustomCells(bool binary)
    {
        var doc = new CadDocument(); doc.Header.Version = ACadVersion.AC1018;
        var style = doc.TableStyles[TableStyle.DefaultName]; var rows = new[] { style.DataCellStyle, style.TitleCellStyle, style.HeaderCellStyle };
        for (var i = 0; i < rows.Length; i++) { rows[i].TextHeight = 3.5 + i; rows[i].TextStyle = new TextStyle("ROW_" + i); rows[i].BackgroundColor = new Color((short)(i + 1)); }
        for (var generation = 0; generation < 2; generation++)
        {
            using var output = new MemoryStream(); DxfWriter.Write(output, doc, binary); using var input = new MemoryStream(output.ToArray()); doc = DxfReader.Read(input);
            style = doc.TableStyles[TableStyle.DefaultName]; Assert.Empty(style.CellStyles); rows = new[] { style.DataCellStyle, style.TitleCellStyle, style.HeaderCellStyle };
            for (var i = 0; i < rows.Length; i++) { Assert.Equal(3.5 + i, rows[i].TextHeight); Assert.Same(doc.TextStyles["ROW_" + i], rows[i].TextStyle); Assert.Equal(new Color((short)(i + 1)), rows[i].BackgroundColor); }
        }
    }
    [Fact]
    public void NoneDxfFailureLeavesSourceUnchanged()
    {
        var doc = new CadDocument(); var style = doc.TableStyles[TableStyle.DefaultName]; style.TitleCellStyle.BackgroundColor = Color.None;
        var before = Snapshot(style); using var output = new MemoryStream();
        var error = Assert.Throws<InvalidOperationException>(() => DxfWriter.Write(output, doc)); Assert.Contains("None", error.Message); Assert.Equal(before, Snapshot(style));
    }
    [Fact]
    public void GenericDxfNoneRefusesInsteadOfAliasingByEntity()
    {
        var doc = new CadDocument(); doc.ModelSpace.Entities.Add(new ACadSharp.Entities.Line { Color = Color.None });
        using var stream = new MemoryStream(); Assert.ThrowsAny<Exception>(() => DxfWriter.Write(stream, doc));
    }
}
