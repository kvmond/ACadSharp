using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

public class AttributeContextSerializationTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void NonEmbeddedAc1032ContextKeepsDistinctCoordinatesAndReferences(bool isDefault, short version)
    {
        var document = Create(); var source = Context(document);
        source.Default = isDefault; source.Version = version; source.Rotation = -.731;
        source.AttachmentPoint = (AttachmentPointType)2;
        source.InsertPoint = new XYZ(12.125, -83.75, 0); source.AlignmentPoint = new XYZ(-16.5, 91.25, 0);
        var handle = source.Handle; var owner = source.Owner.Handle; var scale = source.Scale.Handle;
        foreach (var format in new[] { "dwg", "ascii", "binary" })
        {
            var current = document;
            for (var generation = 0; generation < 2; generation++)
            {
                using var bytes = new MemoryStream();
                if (format == "dwg") DwgWriter.Write(bytes, current); else DxfWriter.Write(bytes, current, format == "binary");
                using var input = new MemoryStream(bytes.ToArray());
                current = format == "dwg" ? DwgReader.Read(input) : DxfReader.Read(input);
                var actual = current.GetCadObject<MTextAttributeObjectContextData>(handle);
                Assert.NotNull(actual); Assert.Equal(version, actual.Version); Assert.Equal(isDefault, actual.Default);
                Assert.Equal(source.InsertPoint, actual.InsertPoint); Assert.Equal(source.AlignmentPoint, actual.AlignmentPoint);
                // The existing DXF angle convention is normalized to [0, 2*pi); DWG keeps the raw angle.
                var expectedAngle = format == "dwg" ? source.Rotation : source.Rotation + 2 * Math.PI;
                Assert.Equal(expectedAngle, actual.Rotation, 12); Assert.Equal(source.AttachmentPoint, actual.AttachmentPoint);
                Assert.False(actual.Value290); Assert.Equal(owner, actual.Owner.Handle); Assert.Equal(scale, actual.Scale.Handle);
                Assert.Same(current.GetCadObject(scale), actual.Scale); Assert.Contains(actual.Scale, current.Scales);
                Assert.Equal(source.Reactors.Select(r => r.Handle), actual.Reactors.Select(r => r.Handle));
            }
        }
        Assert.Same(document, source.Document); Assert.Equal(handle, source.Handle);
    }

    [Theory]
    [InlineData("embedded")]
    [InlineData("elevation")]
    [InlineData("nonfinite")]
    [InlineData("version")]
    [InlineData("legacy")]
    public void UnsupportedDwgFormsFailWithoutChangingSource(string problem)
    {
        var doc = Create(); var context = Context(doc);
        switch (problem)
        {
            case "embedded": context.Value290 = true; break;
            case "elevation": context.InsertPoint = new XYZ(1, 2, 3); break;
            case "nonfinite": context.Rotation = double.NaN; break;
            case "version": context.Version = 8; break;
            case "legacy": doc.Header.Version = ACadVersion.AC1027; break;
        }
        var point = context.InsertPoint; var handle = context.Handle; var scale = context.Scale; var seed = doc.Header.HandleSeed;
        using var output = new MemoryStream(); Assert.ThrowsAny<Exception>(() => DwgWriter.Write(output, doc));
        Assert.Equal(point, context.InsertPoint); Assert.Equal(handle, context.Handle); Assert.Same(scale, context.Scale);
        Assert.Equal(seed, doc.Header.HandleSeed); Assert.Same(context, doc.GetCadObject(handle));
    }

    [Fact]
    public void DxfCoordinatesUseTheirOwnGroupsAndEmbeddedDataIsRejected()
    {
        var doc = Create(); var context = Context(doc);
        context.InsertPoint = new XYZ(12, 23, 0); context.AlignmentPoint = new XYZ(34, 45, 0);
        using var bytes = new MemoryStream(); DxfWriter.Write(bytes, doc);
        var text = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        var record = text.Substring(text.IndexOf("ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS\r\n  5", StringComparison.Ordinal));
        Assert.Matches(@"(?m)^\s*10\r?\n12\r?\n\s*20\r?\n23\r?$", record);
        Assert.Matches(@"(?m)^\s*11\r?\n34\r?\n\s*21\r?\n45\r?$", record);
        context.Value290 = true;
        using var rejected = new MemoryStream(); Assert.Throws<NotSupportedException>(() => DxfWriter.Write(rejected, doc));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-type")]
    [InlineData("zero")]
    public void InvalidDxfScaleReportsAnErrorAndCannotBeWrittenAsAValidDwg(string problem)
    {
        var document = Create();
        using var bytes = new MemoryStream(); DxfWriter.Write(bytes, document);
        var lines = System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Replace("\r\n", "\n").Split('\n');
        var contextRecord = false; var replaced = 0;
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (lines[i].Trim() == "0") contextRecord = lines[i + 1].Trim() == "ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS";
            if (!contextRecord || lines[i].Trim() != "340") continue;
            lines[i + 1] = problem == "zero" ? "0" : problem == "wrong-type" ? document.Layers["0"].Handle.ToString("X") : "FFFFFFF";
            replaced++;
        }
        Assert.Equal(1, replaced);
        var notifications = new List<NotificationEventArgs>();
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        var read = DxfReader.Read(input, (_, e) => notifications.Add(e));
        var error = Assert.Single(notifications, n => n.NotificationType == NotificationType.Error && n.Message.Contains("invalid SCALE"));
        Assert.IsType<InvalidDataException>(error.Exception);
        // The existing model has a detached default Scale; it must not become a guessed registered reference.
        Assert.Null(Context(read).Scale.Document); Assert.Equal(0UL, Context(read).Scale.Handle);
        using var output = new MemoryStream(); Assert.Throws<InvalidDataException>(() => DwgWriter.Write(output, read));
        using var dxfOutput = new MemoryStream(); Assert.Throws<InvalidDataException>(() => DxfWriter.Write(dxfOutput, read));
        Assert.Same(document, Context(document).Scale.Document);
    }

    private static CadDocument Create()
    {
        var doc = new CadDocument(ACadVersion.AC1032); var owner = new CadDictionary { Name = "CONTEXTS" };
        doc.RootDictionary.Add(owner); var scale = new Scale { Name = "SYNTHETIC", PaperUnits = 2, DrawingUnits = 7 };
        doc.Scales.Add(scale); var context = new MTextAttributeObjectContextData { Name = "A", Scale = scale };
        owner.Add(context); context.AddReactor(owner); return doc;
    }
    private static MTextAttributeObjectContextData Context(CadDocument doc) =>
        doc.RootDictionary.GetEntry<CadDictionary>("CONTEXTS").GetEntry<MTextAttributeObjectContextData>("A");
}
