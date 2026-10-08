using System;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using CSMath.Extensions;
using CSMath.Geometry;
using Xunit;

namespace ACadSharp.Tests.Entities;

/// <summary>Synthetic regressions for the XY center used by the actual serialized angular measurement.</summary>
public class AngularDimensionMeasurementTests
{
    private static XYZ Point(double degrees, double radius, double z = 0)
    {
        double angle = degrees * Math.PI / 180;
        return new XYZ(17 + radius * Math.Cos(angle), -23 + radius * Math.Sin(angle), z);
    }

    private static DimensionAngular2Line Dimension(double position, bool firstReverse, bool secondReverse, double z = 0)
    {
        return new DimensionAngular2Line
        {
            FirstPoint = Point(20, firstReverse ? 100 : -100, z), SecondPoint = Point(20, firstReverse ? -100 : 100, z),
            AngleVertex = Point(100, secondReverse ? 100 : -100, z), DefinitionPoint = Point(100, secondReverse ? -100 : 100, z),
            DimensionArc = Point(position, 50, z), Normal = XYZ.AxisZ,
        };
    }

    [Theory]
    [InlineData(60, 80)] [InlineData(150, 100)] [InlineData(240, 80)] [InlineData(330, 100)]
    public void CoplanarCenterAndMeasurementKeepEverySectorAndEndpointDirection(double position, double degrees)
    {
        foreach (bool first in new[] { false, true })
            foreach (bool second in new[] { false, true })
                foreach (double z in new[] { 0d, 7 })
                {
                    var dimension = Dimension(position, first, second, z);
                    Assert.Equal(17, dimension.Center.X, 10); Assert.Equal(-23, dimension.Center.Y, 10); Assert.Equal(z, dimension.Center.Z);
                    Assert.Equal(degrees * Math.PI / 180, dimension.Measurement, 12);
                    Assert.Equal(dimension.Measurement, ((DimensionAngular2Line)dimension.Clone()).Measurement, 12);
                }
    }

    [Fact]
    public void ParallelAndZeroLengthDefinitionsHaveNoFiniteCenterAndOther3DUsesItsExistingPath()
    {
        var dimension = Dimension(60, false, false);
        dimension.DefinitionPoint = dimension.AngleVertex + (dimension.SecondPoint - dimension.FirstPoint);
        Assert.True(dimension.Center.IsNaN());
        dimension.SecondPoint = dimension.FirstPoint;
        Assert.True(dimension.Center.IsNaN());
        dimension = Dimension(60, false, false); dimension.SecondPoint += XYZ.AxisZ;
        var first = Line3D.FromPoints(dimension.DefinitionPoint, dimension.AngleVertex);
        var second = Line3D.FromPoints(dimension.FirstPoint, dimension.SecondPoint);
        var expected = first.FindIntersection(second);
        if (expected.IsNaN()) Assert.True(dimension.Center.IsNaN());
        else Assert.Equal(expected, dimension.Center);
    }

    [Theory]
    [InlineData(ACadVersion.AC1018)] [InlineData(ACadVersion.AC1032)]
    public void SerializedMeasurementAndDefinitionsSurviveTwoDwgAndDxfGenerations(ACadVersion version)
    {
        var document = new CadDocument(version); document.Header.PlotStyleMode = 1;
        foreach (var style in document.MLineStyles) foreach (var element in style.Elements) element.LineType = document.LineTypes["Continuous"];
        var dimension = Dimension(150, false, false);
        // This native measurement test does not regenerate or claim an AutoCAD picture oracle.
        var picture = new BlockRecord("*DTEST"); picture.Entities.Add(new Line(XYZ.Zero, XYZ.AxisX)); dimension.Block = picture;
        document.Entities.Add(dimension);
        var original = new[] { dimension.FirstPoint, dimension.SecondPoint, dimension.AngleVertex, dimension.DefinitionPoint, dimension.DimensionArc };
        ulong handle = dimension.Handle;
        for (int generation = 0; generation < 2; generation++)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new DwgWriter(output, document) { Configuration = new DwgWriterConfiguration { UpdateDimensionsInModel = false, UpdateDimensionsInBlocks = false } }) writer.Write();
                document = DwgReader.Read(new MemoryStream(output.ToArray()));
            }
            var stored = document.Entities.OfType<DimensionAngular2Line>().Single();
            Assert.Equal(handle, stored.Handle); Assert.Equal(100 * Math.PI / 180, stored.Measurement, 12);
            Assert.Equal(original, new[] { stored.FirstPoint, stored.SecondPoint, stored.AngleVertex, stored.DefinitionPoint, stored.DimensionArc });
            using (var output = new MemoryStream())
            {
                using (var writer = new DxfWriter(output, document)) writer.Write();
                byte[] bytes = output.ToArray();
                // The existing DXF writer omits optional DIMENSION code 42; verify the
                // original definitions and their independently expected measurement instead.
                var dxf = DxfReader.Read(new MemoryStream(bytes)).Entities.OfType<DimensionAngular2Line>().Single();
                Assert.Equal(stored.Measurement, dxf.Measurement, 12);
                Assert.Equal(original, new[] { dxf.FirstPoint, dxf.SecondPoint, dxf.AngleVertex, dxf.DefinitionPoint, dxf.DimensionArc });
            }
        }
    }
}
