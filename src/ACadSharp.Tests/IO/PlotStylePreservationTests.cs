using System;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.Header;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using Xunit;

namespace ACadSharp.Tests.IO;

// Synthetic inputs only. Office AutoCAD verification is a separate acceptance gate.
public class PlotStylePreservationTests
{
    [Theory]
    [InlineData(ACadVersion.AC1018, "dwg")]
    [InlineData(ACadVersion.AC1032, "dwg")]
    [InlineData(ACadVersion.AC1018, "ascii")]
    [InlineData(ACadVersion.AC1032, "ascii")]
    [InlineData(ACadVersion.AC1018, "binary")]
    [InlineData(ACadVersion.AC1032, "binary")]
    public void DictionaryHeaderLayersAndEntitiesKeepTheirReferencesForTwoGenerations(ACadVersion version, string format)
    {
        var source = Create(version);
        var expected = State(source);
        var current = source;
        for (var generation = 0; generation < 2; generation++)
        {
            current = Copy(current, format);
            AssertState(current, expected);
            var dwg = current;
            for (var saved = 0; saved < 2; saved++) { dwg = Copy(dwg, "dwg"); AssertState(dwg, expected); }
        }
        AssertState(source, expected);
    }

    [Fact]
    public void DefaultPointerReplacementNullAndCloneNeverUnregisterDictionaryMembers()
    {
        var doc = Create(ACadVersion.AC1032); var dictionary = Dictionary(doc);
        var normal = dictionary.GetEntry<AcdbPlaceHolder>("Normal"); var named = dictionary.GetEntry<AcdbPlaceHolder>("Style 1");
        var seed = doc.Header.HandleSeed;
        dictionary.DefaultEntry = normal; dictionary.DefaultEntry = named; dictionary.DefaultEntry = null; dictionary.DefaultEntry = normal;
        Assert.Same(normal, doc.GetCadObject(normal.Handle)); Assert.Same(named, doc.GetCadObject(named.Handle));
        Assert.Equal(seed, doc.Header.HandleSeed);
        var clone = (CadDictionaryWithDefault)dictionary.Clone();
        Assert.NotSame(normal, clone.DefaultEntry); Assert.Same(clone.GetEntry<AcdbPlaceHolder>("Normal"), clone.DefaultEntry);
        Assert.Contains(clone, clone.DefaultEntry.Reactors); Assert.DoesNotContain(dictionary, clone.DefaultEntry.Reactors);
        Assert.Same(dictionary, normal.Owner); Assert.Same(doc, normal.Document);
        var target = new CadDocument(); target.RootDictionary.Add(clone);
        Assert.Same(target, clone.DefaultEntry.Document); Assert.Same(clone.DefaultEntry, target.GetCadObject(clone.DefaultEntry.Handle));
        Assert.Same(normal, doc.GetCadObject(normal.Handle));
    }

    [Fact]
    public void CloneMatchPropertiesAndRegistrationUseExistingTargetEntriesWithoutStealingTheSource()
    {
        var source = Create(ACadVersion.AC1032); var original = source.Entities.Single(e => e.PlotStyleType == EntityPlotStyleType.ByObjectId);
        var expected = State(source); var seed = source.Header.HandleSeed;
        var same = (Entity)original.Clone(); source.Entities.Add(same); Assert.Same(original.PlotStyle, same.PlotStyle); source.Entities.Remove(same);
        var target = Create(ACadVersion.AC1032); var copy = (Entity)original.Clone(); target.Entities.Add(copy);
        Assert.Same(Dictionary(target).GetEntry<AcdbPlaceHolder>("Style 1"), copy.PlotStyle);
        var matched = new Line(); target.Entities.Add(matched); matched.MatchProperties(original); Assert.Same(copy.PlotStyle, matched.PlotStyle);
        target.Entities.Remove(copy); Assert.Same(original.PlotStyle, source.GetCadObject(original.PlotStyle.Handle));
        Assert.Equal(expected, State(source)); Assert.True(source.Header.HandleSeed >= seed);
        var missing = new CadDocument(); var unresolved = (Entity)original.Clone(); missing.Entities.Add(unresolved);
        Assert.Same(original.PlotStyle, unresolved.PlotStyle); Assert.Same(missing, unresolved.Document);
        Assert.Same(unresolved, missing.GetCadObject(unresolved.Handle));
        Assert.Throws<InvalidDataException>(() => Copy(missing, "dwg"));
        missing.Entities.Remove(unresolved); Assert.Null(unresolved.Document);
        Assert.Same(original.PlotStyle, source.GetCadObject(original.PlotStyle.Handle));
    }

    [Theory]
    [InlineData("dwg")]
    [InlineData("ascii")]
    [InlineData("binary")]
    public void MissingWrongOwnerInconsistentAndLegacyReferencesRefuseToWrite(string format)
    {
        var doc = Create(ACadVersion.AC1032); var line = doc.Entities.Single(e => e.PlotStyleType == EntityPlotStyleType.ByObjectId);
        line.PlotStyle = null; Assert.Throws<InvalidDataException>(() => Copy(doc, format));
        Assert.Throws<InvalidOperationException>(() => line.PlotStyle = new AcdbPlaceHolder { Name = "Detached" });
        line.PlotStyle = Dictionary(doc).GetEntry<AcdbPlaceHolder>("Style 1"); line.PlotStyleType = EntityPlotStyleType.ByLayer;
        Assert.Throws<InvalidDataException>(() => Copy(doc, format));
        line.PlotStyleType = EntityPlotStyleType.ByObjectId; doc.Header.Version = ACadVersion.AC1014;
        Assert.Throws<InvalidDataException>(() => Copy(doc, format));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EveryHeaderModeKeepsItsConditionalHandle(int mode)
    {
        var source = Create(ACadVersion.AC1032);
        source.Header.CurrentEntityPlotStyle = (EntityPlotStyleType)mode;
        if (mode != 3) source.Header.CurrentEntityPlotStyleReference = null;
        foreach (var format in new[] { "ascii", "binary", "dwg" })
        {
            var copy = Copy(Copy(source, format), format);
            Assert.Equal((EntityPlotStyleType)mode, copy.Header.CurrentEntityPlotStyle);
            Assert.Equal(source.Header.CurrentEntityPlotStyleHandle, copy.Header.CurrentEntityPlotStyleHandle);
        }
    }

    [Theory]
    [InlineData("380\n1\n", EntityPlotStyleType.ByBlock, 0UL)]
    [InlineData("390\nBAD\n", EntityPlotStyleType.ByObjectId, 0xBADUL)]
    [InlineData("380\n1\n390\nBAD\n", EntityPlotStyleType.ByBlock, 0xBADUL)]
    [InlineData("390\nBAD\n380\n1\n", EntityPlotStyleType.ByBlock, 0xBADUL)]
    public void IndependentDxfCodesPreserveTypeAndUnresolvedHandle(string codes, EntityPlotStyleType mode, ulong handle)
    {
        // DXF common group 380 is reserved for the plot-style type; 390 is the hard pointer.
        // This fixture is hand-authored, not produced by this writer. ByBlock has no office oracle yet.
        var input = "0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1032\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n0\nLINE\n5\n123\n100\nAcDbEntity\n8\n0\n" + codes
            + "100\nAcDbLine\n10\n0\n20\n0\n30\n0\n11\n1\n21\n0\n31\n0\n0\nENDSEC\n0\nEOF\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input));
        var doc = DxfReader.Read(stream); doc.CreateDefaults(); var entity = Assert.Single(doc.Entities);
        Assert.Equal(mode, entity.PlotStyleType); Assert.Equal(handle, entity.PlotStyleHandle);
        if (handle != 0) Assert.Throws<InvalidDataException>(() => Copy(doc, "dwg"));
        else Assert.Equal(mode, Assert.Single(Copy(doc, "dwg").Entities).PlotStyleType);
    }

    [Theory]
    [InlineData("layer")] [InlineData("entity")] [InlineData("header")] [InlineData("default")]
    public void RemovedPlaceholderCannotBecomeASilentZeroReference(string location)
    {
        var doc = Create(ACadVersion.AC1032); var dict = Dictionary(doc); var named = dict.GetEntry<AcdbPlaceHolder>("Style 1");
        doc.Header.CurrentEntityPlotStyle = EntityPlotStyleType.ByLayer; doc.Header.CurrentEntityPlotStyleReference = null;
        doc.Layers["NAMED"].PlotStyle = null;
        foreach (var entity in doc.Entities) { entity.PlotStyle = null; entity.PlotStyleType = EntityPlotStyleType.ByLayer; }
        if (location == "header") { doc.Header.CurrentEntityPlotStyle = EntityPlotStyleType.ByObjectId; doc.Header.CurrentEntityPlotStyleReference = named; }
        if (location == "layer") doc.Layers["NAMED"].PlotStyle = named;
        if (location == "entity") { var entity = doc.Entities.First(); entity.PlotStyleType = EntityPlotStyleType.ByObjectId; entity.PlotStyle = named; }
        if (location == "default") dict.DefaultEntry = named;
        dict.Remove("Style 1", out _);
        Assert.Null(named.Document);
        foreach (var format in new[] { "dwg", "ascii", "binary" }) Assert.Throws<InvalidDataException>(() => Copy(doc, format));
        Assert.DoesNotContain(dict, e => ReferenceEquals(e, named));
    }

    [Fact]
    public void InvalidSettersLeaveExistingReferencesAndSourceResourcesUntouched()
    {
        var doc = Create(ACadVersion.AC1032); var dictionary = Dictionary(doc); var normal = dictionary.GetEntry<AcdbPlaceHolder>("Normal");
        var wrong = new AcdbPlaceHolder { Name = "Outside" }; doc.RootDictionary.Add(wrong);
        var entity = doc.Entities.First(); var before = State(doc); var seed = doc.Header.HandleSeed;
        Assert.Throws<InvalidDataException>(() => entity.PlotStyle = wrong);
        Assert.Throws<InvalidDataException>(() => doc.Layers["0"].PlotStyle = wrong);
        Assert.Throws<InvalidDataException>(() => doc.Header.CurrentEntityPlotStyleReference = wrong);
        Assert.Throws<InvalidOperationException>(() => entity.PlotStyle = new AcdbPlaceHolder { Name = "Missing" });
        Assert.Equal(before, State(doc)); Assert.Equal(seed, doc.Header.HandleSeed);
        Assert.Same(normal, doc.GetCadObject(normal.Handle)); Assert.Same(doc.RootDictionary, wrong.Owner);
    }

    [Theory]
    [InlineData("ascii")] [InlineData("binary")] [InlineData("dwg")]
    public void NullSeqendEncodingStaysRawInDwgAndMatchesAutoCadOmissionInDxf(string format)
    {
        var doc = Create(ACadVersion.AC1032); var block = new BlockRecord("ATTR");
        block.Entities.Add(new AttributeDefinition { Tag = "A", Value = "1", Height = 1 }); doc.BlockRecords.Add(block);
        var insert = new Insert(block); doc.Entities.Add(insert);
        insert.Attributes.Seqend.PlotStyleType = EntityPlotStyleType.ByObjectId;
        var handle = insert.Attributes.Seqend.Handle; var original = doc;
        for (var generation = 0; generation < 2; generation++)
        {
            doc = Copy(doc, format); var seqend = doc.GetCadObject<Seqend>(handle);
            Assert.Equal(format == "dwg" ? EntityPlotStyleType.ByObjectId : EntityPlotStyleType.ByLayer, seqend.PlotStyleType); Assert.Equal(0UL, seqend.PlotStyleHandle);
        }
        Assert.Equal(EntityPlotStyleType.ByObjectId, original.GetCadObject<Seqend>(handle).PlotStyleType);
    }

    [Theory]
    [InlineData("ascii")] [InlineData("binary")] [InlineData("dwg")]
    public void OwnedSequenceEndNamedPlotStyleIsNotOmitted(string format)
    {
        var doc = Create(ACadVersion.AC1032); var block = new BlockRecord("ATTR");
        block.Entities.Add(new AttributeDefinition { Tag = "A", Value = "1", Height = 1 }); doc.BlockRecords.Add(block);
        var insert = new Insert(block); doc.Entities.Add(insert);
        insert.Attributes.Seqend.PlotStyleType = EntityPlotStyleType.ByObjectId;
        insert.Attributes.Seqend.PlotStyle = Dictionary(doc).GetEntry<AcdbPlaceHolder>("Style 1");
        var handle = insert.Attributes.Seqend.Handle;
        for (var generation = 0; generation < 2; generation++)
        {
            doc = Copy(doc, format); var seqend = doc.GetCadObject<Seqend>(handle);
            Assert.Equal(EntityPlotStyleType.ByObjectId, seqend.PlotStyleType);
            Assert.Same(Dictionary(doc).GetEntry<AcdbPlaceHolder>("Style 1"), seqend.PlotStyle);
        }
    }

    internal static CadDocument Create(ACadVersion version)
    {
        var doc = new CadDocument(version); doc.Header.PlotStyleMode = 0;
        var dictionary = new CadDictionaryWithDefault { Name = "ACAD_PLOTSTYLENAME" };
        var normal = new AcdbPlaceHolder { Name = "Normal" }; var named = new AcdbPlaceHolder { Name = "Style 1" };
        dictionary.Add(normal); dictionary.Add(named); normal.AddReactor(dictionary); named.AddReactor(dictionary); dictionary.DefaultEntry = normal; doc.RootDictionary.Add(dictionary);
        doc.Header.CurrentEntityPlotStyle = EntityPlotStyleType.ByObjectId; doc.Header.CurrentEntityPlotStyleReference = named;
        doc.Layers["0"].PlotStyle = normal; var layer = new Layer("NAMED") { PlotStyle = named }; doc.Layers.Add(layer);
        foreach (var mode in new[] { EntityPlotStyleType.ByLayer, EntityPlotStyleType.ByBlock, EntityPlotStyleType.ByDictionaryDefault, EntityPlotStyleType.ByObjectId })
            doc.Entities.Add(new Line(CSMath.XYZ.Zero, CSMath.XYZ.AxisX) { Layer = layer, PlotStyleType = mode, PlotStyle = mode == EntityPlotStyleType.ByObjectId ? named : null });
        return doc;
    }
    internal static CadDictionaryWithDefault Dictionary(CadDocument doc) => doc.RootDictionary.GetEntry<CadDictionaryWithDefault>("ACAD_PLOTSTYLENAME");
    internal static string State(CadDocument doc) => string.Join("|", new[] { ((short)doc.Header.CurrentEntityPlotStyle).ToString(), doc.Header.CurrentEntityPlotStyleHandle.ToString(), Dictionary(doc).DefaultEntry.Handle.ToString(), doc.Layers["0"].PlotStyleName.ToString(), doc.Layers["NAMED"].PlotStyleName.ToString() }.Concat(doc.Entities.Select(e => $"{e.Handle}:{(short)e.PlotStyleType}:{e.PlotStyleHandle}")));
    private static void AssertState(CadDocument doc, string state)
    {
        Assert.Equal(state, State(doc)); var dictionary = Dictionary(doc);
        Assert.Same(dictionary.GetEntry<AcdbPlaceHolder>("Normal"), dictionary.DefaultEntry);
        Assert.Same(dictionary.GetEntry<AcdbPlaceHolder>("Style 1"), doc.Header.CurrentEntityPlotStyleReference);
        foreach (var entry in dictionary) { Assert.Same(dictionary, entry.Owner); Assert.Same(doc, entry.Document); Assert.Same(entry, doc.GetCadObject(entry.Handle)); Assert.Contains(dictionary, entry.Reactors); }
        foreach (var entity in doc.Entities.Where(e => e.PlotStyle != null)) Assert.Same(doc.GetCadObject(entity.PlotStyleHandle), entity.PlotStyle);
        foreach (var layer in doc.Layers.Where(l => l.PlotStyle != null)) Assert.Same(doc.GetCadObject(layer.PlotStyleName), layer.PlotStyle);
    }
    internal static CadDocument Copy(CadDocument source, string format)
    {
        using var stream = new MemoryStream();
        if (format == "dwg") DwgWriter.Write(stream, source, new DwgWriterConfiguration { CloseStream = false });
        else DxfWriter.Write(stream, source, format == "binary", new DxfWriterConfiguration { CloseStream = false });
        using var input = new MemoryStream(stream.ToArray());
        return format == "dwg" ? DwgReader.Read(input) : DxfReader.Read(input);
    }
}
