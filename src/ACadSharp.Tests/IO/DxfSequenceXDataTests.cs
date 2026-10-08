using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.XData;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>Synthetic ownership boundaries; no third-party drawings or fonts.</summary>
public class DxfSequenceXDataTests
{
    private const string App = "SEQUENCE_DATA";

    private static CadDocument Source(ACadVersion version)
    {
        var d = new CadDocument(version); d.Header.PlotStyleMode = 1;
        var block = new BlockRecord("PART"); block.Entities.Add(new AttributeDefinition { Tag = "TAG", Value = "value", Height = 2 }); d.BlockRecords.Add(block);
        d.Entities.Add(new Insert(block));
        var polyline = new Polyline2D(); polyline.Vertices.Add(new Vertex2D(new XYZ(1, 2, 0))); polyline.Vertices.Add(new Vertex2D(new XYZ(5, 6, 0))); d.Entities.Add(polyline);
        var spatial = new Polyline3D(); spatial.Vertices.Add(new Vertex3D(new XYZ(1, 2, 3))); spatial.Vertices.Add(new Vertex3D(new XYZ(5, 6, 7))); d.Entities.Add(spatial);
        var mesh = new PolyfaceMesh();
        mesh.Vertices.Add(new VertexFaceMesh(XYZ.Zero)); mesh.Vertices.Add(new VertexFaceMesh(XYZ.AxisX)); mesh.Vertices.Add(new VertexFaceMesh(XYZ.AxisY));
        mesh.Faces.Add(new VertexFaceRecord { Index1 = 1, Index2 = 2, Index3 = 3 }); d.Entities.Add(mesh);
        var grid = new PolygonMesh { MVertexCount = 2, NVertexCount = 2 };
        foreach (var point in new[] { XYZ.Zero, XYZ.AxisX, XYZ.AxisY, XYZ.AxisX + XYZ.AxisY }) grid.Vertices.Add(new PolygonMeshVertex(point));
        d.Entities.Add(grid);
        var plain = new BlockRecord("PLAIN"); plain.Entities.Add(new Line(XYZ.Zero, XYZ.AxisX)); d.BlockRecords.Add(plain); d.Entities.Add(new Insert(plain));
        d.Entities.Add(new Line(new XYZ(20, 30, 0), new XYZ(40, 50, 0)));
        var app = new AppId(App); d.AppIds.Add(app);
        var owners = Owners(d);
        for (int i = 0; i < owners.Length; i++)
        {
            var entity = owners[i];
            entity.ExtendedData.Add(app, new ExtendedData(new ExtendedDataRecord[] {
                new ExtendedDataString("owner:" + entity.Handle.ToString("X", CultureInfo.InvariantCulture)),
                new ExtendedDataBinaryChunk(new byte[] { (byte)i, 0, 255 }),
                new ExtendedDataHandle(entity.Handle), new ExtendedDataHandle(owners[(i + 1) % owners.Length].Handle),
                new ExtendedDataLayer(d.Layers["0"].Handle), new ExtendedDataWorldCoordinate(new XYZ(i + 1, i + 2, i + 3)) }));
        }
        return d;
    }

    private static Entity[] Owners(CadDocument d) => d.Entities.SelectMany<Entity, Entity>(e => e switch {
        Insert i when i.HasAttributes => new Entity[] { i }.Concat(i.Attributes).Concat(new Entity[] { i.Attributes.Seqend }),
        Polyline2D p => new Entity[] { p }.Concat(p.Vertices).Concat(new Entity[] { p.Vertices.Seqend }),
        Polyline3D p => new Entity[] { p }.Concat(p.Vertices).Concat(new Entity[] { p.Vertices.Seqend }),
        PolyfaceMesh p => new Entity[] { p }.Concat(p.Vertices).Concat(p.Faces).Concat(new Entity[] { p.Vertices.Seqend }),
        PolygonMesh p => new Entity[] { p }.Concat(p.Vertices).Concat(new Entity[] { p.Vertices.Seqend }),
        _ => new[] { e } }).ToArray();

    private static string[] Values(CadDocument d) => Owners(d).Select(e => e.Handle + ":" + string.Join("|", e.ExtendedData.Get(App).Records.Select(r =>
        ((int)r.Code).ToString(CultureInfo.InvariantCulture) + "=" + (r is ExtendedDataBinaryChunk binary ? Convert.ToBase64String(binary.Value) : r.RawValue.ToString())))).ToArray();

    [Theory]
    [InlineData(ACadVersion.AC1018, 0)] [InlineData(ACadVersion.AC1032, 0)]
    [InlineData(ACadVersion.AC1018, 1)] [InlineData(ACadVersion.AC1032, 1)]
    [InlineData(ACadVersion.AC1018, 2)] [InlineData(ACadVersion.AC1032, 2)]
    public void RootChildAndSequenceEndKeepTheirOwnPayloadAndReferences(ACadVersion version, int format)
    {
        var d = Source(version); var original = d; var expected = Values(d); var owners = Owners(d); var handles = owners.Select(e => e.Handle).ToArray();
        for (int generation = 0; generation < 2; generation++)
        {
            var before = Values(d); var sourceOwners = Owners(d); var seed = d.Header.HandleSeed;
            using var output = new MemoryStream();
            if (format == 0) DwgWriter.Write(output, d); else DxfWriter.Write(output, d, format == 2);
            var bytes = output.ToArray(); Assert.Equal(before, Values(d)); Assert.Equal(seed, d.Header.HandleSeed);
            for (int i = 0; i < sourceOwners.Length; i++) Assert.Same(sourceOwners[i], d.GetCadObject(handles[i]));
            if (format == 1) CheckAsciiBoundaries(bytes, handles);
            using var input = new MemoryStream(bytes); d = format == 0 ? DwgReader.Read(input) : DxfReader.Read(input);
            Assert.Equal(expected, Values(d)); Assert.Equal(handles, Owners(d).Select(e => e.Handle).ToArray());
            var restored = Owners(d);
            for (int i = 0; i < restored.Length; i++)
            {
                var entity = restored[i]; Assert.Single(entity.ExtendedData);
                Assert.Same(d, entity.Document); Assert.Same(entity, d.GetCadObject(entity.Handle));
                Assert.Same(entity.Owner, d.GetCadObject(entity.Owner.Handle));
                Assert.Same(d.AppIds[App], entity.ExtendedData.Single().Key);
                var records = entity.ExtendedData.Get(App).Records; Assert.Equal(6, records.Count);
                Assert.Same(entity, Assert.IsType<ExtendedDataHandle>(records[2]).ResolveReference(d));
                Assert.Same(restored[(i + 1) % restored.Length], Assert.IsType<ExtendedDataHandle>(records[3]).ResolveReference(d));
                Assert.Same(d.Layers["0"], Assert.IsType<ExtendedDataLayer>(records[4]).ResolveReference(d));
            }
            // This bounded synthetic document must not keep a displaced default SEQEND in its map.
            Assert.InRange(d.Header.HandleSeed, 1UL, 1000UL);
            var registeredEnds = Enumerable.Range(1, (int)d.Header.HandleSeed).Select(h => d.GetCadObject((ulong)h)).OfType<Seqend>().ToArray();
            foreach (var end in restored.OfType<Seqend>())
                Assert.Same(end, Assert.Single(registeredEnds, candidate => ReferenceEquals(candidate.Owner, end.Owner)));
        }
        Assert.Equal(expected, Values(original));
        for (int i = 0; i < owners.Length; i++) Assert.Same(owners[i], original.GetCadObject(handles[i]));
    }

    private static void CheckAsciiBoundaries(byte[] bytes, ulong[] handles)
    {
        // Decode record boundaries independently: no reader or XDATA parser is used for this assertion.
        var lines = Encoding.UTF8.GetString(bytes).Replace("\r", "").Split('\n');
        var records = new List<List<(int Code, string Value)>>();
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
            if (code == 0) records.Add(new());
            if (records.Count > 0) records[records.Count - 1].Add((code, lines[i + 1]));
        }
        foreach (var handle in handles)
        {
            var record = Assert.Single(records, r => r.Any(p => p.Code == 5 && p.Value.Trim().Equals(handle.ToString("X", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)));
            Assert.Single(record, p => p.Code == 1001 && p.Value == App);
            Assert.Single(record, p => p.Code == 1000 && p.Value == "owner:" + handle.ToString("X", CultureInfo.InvariantCulture));
        }
    }
}
