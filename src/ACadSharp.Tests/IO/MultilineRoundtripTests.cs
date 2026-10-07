using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

public class MultilineRoundtripTests
{
    [Theory]
    [InlineData(ACadVersion.AC1014, "dwg")]
    [InlineData(ACadVersion.AC1015, "dwg")] [InlineData(ACadVersion.AC1018, "dwg")]
    [InlineData(ACadVersion.AC1024, "dwg")] [InlineData(ACadVersion.AC1027, "dwg")] [InlineData(ACadVersion.AC1032, "dwg")]
    [InlineData(ACadVersion.AC1018, "ascii")] [InlineData(ACadVersion.AC1032, "ascii")]
    [InlineData(ACadVersion.AC1018, "binary")] [InlineData(ACadVersion.AC1032, "binary")]
    public void ElementLinetypesAndColorsKeepTheirOwnValuesAcrossGenerations(ACadVersion version, string format)
    {
        var document = new CadDocument(version);
        foreach (var name in new[] { "Z_DASH", "A_DOT", "M_PATTERN" }) document.LineTypes.Add(new LineType(name));
        var names = new[] { "ByLayer", "ByBlock", "Continuous", "Z_DASH", "A_DOT", "M_PATTERN" };
        var style = new MLineStyle("ELEMENTS") { FillColor = new Color(5), Flags = MLineStyleFlags.FillOn | MLineStyleFlags.StartRoundCap };
        for (var i = 0; i < names.Length; i++) style.AddElement(new MLineStyle.Element { Offset = i - 3, Color = new Color((short)(i + 1)), LineType = document.LineTypes[names[i]] });
        document.MLineStyles.Add(style);
        document.Header.CurrentMLineStyleName = style.Name;
        document.Header.CurrentMultilineScale = -2;
        document.Header.CurrentMultiLineJustification = VerticalAlignmentType.Middle;
        foreach (var flags in new[] { MLineFlags.Has, MLineFlags.Has | MLineFlags.Closed,
            MLineFlags.Has | MLineFlags.NoStartCaps, MLineFlags.Has | MLineFlags.NoEndCaps,
            MLineFlags.Has | MLineFlags.Closed | MLineFlags.NoStartCaps | MLineFlags.NoEndCaps })
        {
            var entity = new MLine { Style = style, Flags = flags, LineType = document.LineTypes[names[document.ModelSpace.Entities.Count]] };
            foreach (var point in new[] { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0), new CSMath.XYZ(10, 10, 0) })
            {
                var vertex = new MLine.Vertex { Position = point, Direction = CSMath.XYZ.AxisX, Miter = CSMath.XYZ.AxisY };
                for (var i = 0; i < names.Length; i++) vertex.Segments.Add(new MLine.Vertex.Segment { Parameters = new() { i - 3, 0 } });
                entity.Vertices.Add(vertex);
            }
            document.ModelSpace.Entities.Add(entity);
        }
        var expectedFlags = document.ModelSpace.Entities.OfType<MLine>().Select(e => e.Flags).ToArray();
        var expectedEntityLinetypes = document.ModelSpace.Entities.Select(e => e.LineType.Name).ToArray();
        for (var generation = 0; generation < 2; generation++)
        {
            using var output = new MemoryStream();
            if (format == "dwg") DwgWriter.Write(output, document); else DxfWriter.Write(output, document, binary: format == "binary");
            using var input = new MemoryStream(output.ToArray()); document = format == "dwg" ? DwgReader.Read(input) : DxfReader.Read(input);
            style = document.MLineStyles["ELEMENTS"];
            Assert.Equal("ELEMENTS", document.Header.CurrentMLineStyleName); Assert.Same(style, document.Header.CurrentMLineStyle);
            Assert.Equal(-2, document.Header.CurrentMultilineScale); Assert.Equal(VerticalAlignmentType.Middle, document.Header.CurrentMultiLineJustification);
            Assert.Equal(expectedFlags, document.ModelSpace.Entities.OfType<MLine>().Select(e => e.Flags));
            Assert.Equal(expectedEntityLinetypes, document.ModelSpace.Entities.Select(e => e.LineType.Name));
            Assert.Equal(names, style.Elements.Select(e => e.LineType.Name)); Assert.Equal(Enumerable.Range(1, 6), style.Elements.Select(e => (int)e.Color.Index));
            Assert.Equal(5, style.FillColor.Index); Assert.Equal(MLineStyleFlags.FillOn | MLineStyleFlags.StartRoundCap, style.Flags);
            Assert.Equal(new[] { "ByLayer", "ByLayer" }, document.MLineStyles["Standard"].Elements.Select(e => e.LineType.Name));
            Assert.All(style.Elements, element => Assert.Same(document.LineTypes[element.LineType.Name], element.LineType));
        }
    }

    [Fact]
    public void CloningVerticesDoesNotClearOrShareOriginalElementParameters()
    {
        var source = new MLine(); var vertex = new MLine.Vertex { Position = new(3, 4, 0), Direction = new(1, 0, 0), Miter = new(0, 1, 0) };
        var segment = new MLine.Vertex.Segment(); segment.Parameters.AddRange(new[] { .5, 0, 2, 5 }); segment.AreaFillParameters.AddRange(new[] { 1d, 3d });
        vertex.Segments.Add(segment); source.Vertices.Add(vertex);
        var copy = (MLine)source.Clone();
        Assert.Single(vertex.Segments); var cloned = Assert.Single(Assert.Single(copy.Vertices).Segments);
        Assert.Equal(segment.Parameters, cloned.Parameters); Assert.Equal(segment.AreaFillParameters, cloned.AreaFillParameters);
        Assert.NotSame(vertex.Segments, copy.Vertices[0].Segments); Assert.NotSame(segment.Parameters, cloned.Parameters);
        cloned.Parameters[0] = 99; cloned.AreaFillParameters.Clear();
        Assert.Equal(.5, segment.Parameters[0]); Assert.Equal(new[] { 1d, 3d }, segment.AreaFillParameters);
    }
}
