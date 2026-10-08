using ACadSharp.Classes;
using ACadSharp.Entities;
using ACadSharp.IO.DXF;
using ACadSharp.IO.Templates;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>Synthetic reactor sequences, including deliberately malformed references.</summary>
public class ReactorSerializationTests
{
	private static byte[] write(CadDocument document, int format)
	{
		using var stream = new MemoryStream();
		if (format == 0) DwgWriter.Write(stream, document); else DxfWriter.Write(stream, document, format == 2);
		return stream.ToArray();
	}
	private static CadDocument read(byte[] bytes, int format)
	{
		using var stream = new MemoryStream(bytes);
		return format == 0 ? DwgReader.Read(stream) : DxfReader.Read(stream);
	}
	private static ulong[] handles(CadObject obj) => obj.Reactors.Select(r => r.Handle).ToArray();
	private static CadDocument document(ACadVersion version)
	{
		var doc = new CadDocument(version);
		doc.Entities.Add(new Line(XYZ.Zero, XYZ.AxisX)); doc.Entities.Add(new Line(XYZ.AxisY, XYZ.AxisZ));
		doc.RootDictionary.Add(new XRecord("A")); doc.RootDictionary.Add(new XRecord("B"));
		return doc;
	}
	[Theory]
	[InlineData(ACadVersion.AC1018, 0)] [InlineData(ACadVersion.AC1018, 1)] [InlineData(ACadVersion.AC1018, 2)]
	[InlineData(ACadVersion.AC1032, 0)] [InlineData(ACadVersion.AC1032, 1)] [InlineData(ACadVersion.AC1032, 2)]
	public void RepeatedReactorsAndGroupMembersKeepInputOrderAcrossTwoGenerations(ACadVersion version, int format)
	{
		var doc = document(version); var first = doc.Entities.First(); var second = doc.Entities.Last();
		var a = doc.RootDictionary.GetEntry<XRecord>("A"); var b = doc.RootDictionary.GetEntry<XRecord>("B");
		first.AddReactor(a); var g = doc.Groups.CreateGroup("G", new[] { second, first });
		first.AddReactor(b); first.AddReactor(g); first.AddReactor(a);
		doc.Groups.CreateGroup("H", new[] { first }); g.Add(first);
		var expected = handles(first); var secondExpected = handles(second); var members = g.Entities.Select(e => e.Handle).ToArray();
		var firstHandle = first.Handle; var secondHandle = second.Handle;
		for (int generation = 0; generation < 2; generation++)
		{
			var bytes = write(doc, format);
			Assert.Equal(expected, handles(first)); Assert.Equal(members, doc.Groups["G"].Entities.Select(e => e.Handle));
			doc = read(bytes, format); first = doc.GetCadObject<Entity>(firstHandle); second = doc.GetCadObject<Entity>(secondHandle);
			Assert.Equal(expected, handles(first)); Assert.Equal(secondExpected, handles(second));
			Assert.Equal(members, doc.Groups["G"].Entities.Select(e => e.Handle));
			Assert.All(first.Reactors, r => Assert.Same(r, doc.GetCadObject(r.Handle)));
		}
	}
	[Theory]
	[InlineData(0)] [InlineData(1)] [InlineData(2)]
	public void ReaderDoesNotSynthesizeAGroupReactorMissingFromTheFile(int format)
	{
		var doc = document(ACadVersion.AC1032); var entity = doc.Entities.First();
		var group = doc.Groups.CreateGroup("G", new[] { entity }); entity.RemoveReactor(group);
		doc = read(write(doc, format), format);
		Assert.Same(doc.Entities.First(), Assert.Single(doc.Groups["G"].Entities));
		Assert.Empty(doc.Entities.First().Reactors);
	}
	[Theory]
	[InlineData(0)] [InlineData(1)] [InlineData(2)]
	public void ForeignOrDetachedReactorsRefuseWithoutCleaningTheSource(int format)
	{
		foreach (var attached in new[] { false, true })
		{
			var doc = document(ACadVersion.AC1032); var entity = doc.Entities.First();
			var foreign = new XRecord("FOREIGN"); if (attached) new CadDocument().RootDictionary.Add(foreign);
			entity.AddReactor(doc.RootDictionary.GetEntry<XRecord>("A")); entity.AddReactor(foreign); entity.AddReactor(foreign);
			var original = entity.Reactors.ToArray(); var foreignHandle = foreign.Handle; var foreignDocument = foreign.Document;
			Assert.Throws<InvalidDataException>(() => write(doc, format));
			Assert.Equal(original, entity.Reactors); Assert.Equal(foreignHandle, foreign.Handle); Assert.Same(foreignDocument, foreign.Document);
		}
	}
	[Theory]
	[InlineData(ACadVersion.AC1018, false)] [InlineData(ACadVersion.AC1018, true)]
	[InlineData(ACadVersion.AC1032, false)] [InlineData(ACadVersion.AC1032, true)]
	public void DxfTableKeepsDictionaryAndRepeatedReactorGroupsInEitherOrder(ACadVersion version, bool reverse)
	{
		var doc = document(version); var table = doc.Layers;
		var dictionary = table.CreateExtendedDictionary(); dictionary.Add(new XRecord("META"));
		var a = doc.RootDictionary.GetEntry<XRecord>("A"); var b = doc.RootDictionary.GetEntry<XRecord>("B");
		var lines = Encoding.UTF8.GetString(write(doc, 1)).Replace("\r\n", "\n").Split('\n').ToList();
		var handle = Enumerable.Range(0, lines.Count / 2).Single(i => lines[2 * i].Trim() == "5" && lines[2 * i + 1].Trim() == table.Handle.ToString("X")) * 2;
		var start = handle + 2; var end = start;
		while (lines[end].Trim() != "100") end += 2;
		lines.RemoveRange(start, end - start);
		var dictionaryGroup = new[] { "102", "{ACAD_XDICTIONARY", "360", dictionary.Handle.ToString("X"), "102", "}" };
		var reactors = new[] { "102", "{ACAD_REACTORS", "330", a.Handle.ToString("X"), "330", b.Handle.ToString("X"), "330", a.Handle.ToString("X"), "102", "}" };
		var repeated = new[] { "102", "{ACAD_REACTORS", "330", b.Handle.ToString("X"), "102", "}" };
		// These raw 102/330/360 groups are independent of the writer's ordering.
		lines.InsertRange(start, (reverse ? reactors.Concat(dictionaryGroup) : dictionaryGroup.Concat(reactors)).Concat(repeated).Concat(new[] { "330", "0" }));
		doc = read(Encoding.UTF8.GetBytes(string.Join("\n", lines)), 1);
		for (int generation = 0; generation < 3; generation++)
		{
			Assert.Equal(new[] { a.Handle, b.Handle, a.Handle, b.Handle }, handles(doc.Layers));
			Assert.Equal(dictionary.Handle, doc.Layers.XDictionary.Handle); Assert.NotNull(doc.Layers.XDictionary.GetEntry<XRecord>("META"));
			if (generation < 2) doc = read(write(doc, 1), 1);
		}
	}
	[Theory]
	[InlineData("0")] [InlineData("FFFFFFF")]
	public void UnresolvedInputReactorReportsAnErrorInsteadOfSilentLoss(string missing)
	{
		var doc = document(ACadVersion.AC1032); var entity = doc.Entities.First();
		entity.AddReactor(doc.RootDictionary.GetEntry<XRecord>("A"));
		var lines = Encoding.UTF8.GetString(write(doc, 1)).Replace("\r\n", "\n").Split('\n');
		int start = Array.IndexOf(lines, "{ACAD_REACTORS"); Assert.True(start >= 0);
		Assert.Equal("330", lines[start + 1].Trim()); lines[start + 2] = missing;
		var messages = new List<NotificationEventArgs>(); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
		DxfReader.Read(stream, (_, e) => messages.Add(e));
		Assert.Contains(messages, e => e.NotificationType == NotificationType.Error && e.Message.Contains("Reactor") && e.Exception is InvalidDataException);
	}
	[Theory]
	[InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
	public void KnownUnknownReactorRespectsExplicitReaderConfiguration(bool entity, bool keep)
	{
		var builder = new DxfDocumentBuilder(ACadVersion.AC1032, new CadDocument(), new DxfReaderConfiguration
			{ KeepUnknownEntities = keep, KeepUnknownNonGraphicalObjects = keep });
		var cls = new DxfClass { DxfName = "SYNTHETIC_REACTOR", CppClassName = "SyntheticReactor", IsAnEntity = entity };
		CadObject target = entity ? new UnknownEntity(cls) : new UnknownNonGraphicalObject(cls); target.Handle = 2000;
		builder.AddTemplate(entity ? (CadTemplate)new CadUnknownEntityTemplate((UnknownEntity)target) : new CadUnknownNonGraphicalObjectTemplate((UnknownNonGraphicalObject)target));
		var owner = new CadNonGraphicalObjectTemplate(new XRecord("OWNER") { Handle = 1000 }); owner.ReactorsHandles.Add(target.Handle);
		var messages = new List<NotificationEventArgs>(); builder.OnNotification += (_, e) => messages.Add(e);
		owner.Build(builder);
		Assert.DoesNotContain(messages, e => e.NotificationType == NotificationType.Error);
		if (keep) Assert.Same(target, Assert.Single(owner.CadObject.Reactors));
		else { Assert.Empty(owner.CadObject.Reactors); Assert.Contains(messages, e => e.NotificationType == NotificationType.Warning && e.Message.Contains("configuration")); }
	}
	[Theory]
	[InlineData(ACadVersion.AC1018, 0)] [InlineData(ACadVersion.AC1018, 1)] [InlineData(ACadVersion.AC1018, 2)]
	[InlineData(ACadVersion.AC1032, 0)] [InlineData(ACadVersion.AC1032, 1)] [InlineData(ACadVersion.AC1032, 2)]
	public void ImageOwnedReactorStaysRegisteredWithoutRegenerationOrReordering(ACadVersion version, int format)
	{
		var doc = document(version);
		var image = new RasterImage { Definition = new ImageDefinition("IMAGE") { FileName = "synthetic.png", Size = new XY(10, 20) }, Size = new XY(10, 20) };
		image.ClipBoundaryVertices.Add(new XY(-0.5, -0.5)); image.ClipBoundaryVertices.Add(new XY(9.5, 19.5));
		doc.Entities.Add(image); doc.UpdateImageReactors();
		var reactor = image.DefinitionReactor; var reactorHandle = reactor.Handle;
		image.Definition.AddReactor(doc.RootDictionary.GetEntry<XRecord>("A")); image.Definition.AddReactor(reactor);
		var expected = handles(image.Definition); var imageHandle = image.Handle;
		for (int generation = 0; generation < 2; generation++)
		{
			var seed = doc.Header.HandleSeed; var objects = image.Definition.Reactors.ToArray();
			var bytes = write(doc, format);
			Assert.Equal(seed, doc.Header.HandleSeed); Assert.Same(reactor, image.DefinitionReactor);
			Assert.Equal(objects, image.Definition.Reactors); Assert.Equal(expected, handles(image.Definition));
			doc = read(bytes, format); image = doc.GetCadObject<RasterImage>(imageHandle); reactor = image.DefinitionReactor;
			Assert.Equal(reactorHandle, reactor.Handle); Assert.Same(doc, reactor.Document);
			Assert.Same(reactor, doc.GetCadObject(reactor.Handle)); Assert.Same(image, reactor.Owner); Assert.Same(image, reactor.Image);
			Assert.Equal(expected, handles(image.Definition)); Assert.All(image.Definition.Reactors, r => Assert.Same(r, doc.GetCadObject(r.Handle)));
		}
	}
	[Fact]
	public void ImageReactorCloneAndTransferDoNotShareOrOrphanOwnedObjects()
	{
		var source = document(ACadVersion.AC1032); var image = new RasterImage { Definition = new ImageDefinition("IMAGE") { FileName = "synthetic.png" } };
		source.Entities.Add(image); source.UpdateImageReactors(); var reactor = image.DefinitionReactor; var handle = reactor.Handle;
		var clone = (RasterImage)image.Clone();
		Assert.NotSame(reactor, clone.DefinitionReactor); Assert.Null(clone.DefinitionReactor.Document);
		Assert.Same(clone, clone.DefinitionReactor.Owner); Assert.Same(clone, clone.DefinitionReactor.Image);
		Assert.Same(image, reactor.Owner); Assert.Same(source, reactor.Document);
		var target = new CadDocument(); target.Entities.Add(clone); target.UpdateImageReactors();
		Assert.Same(clone.DefinitionReactor, target.GetCadObject(clone.DefinitionReactor.Handle));
		Assert.True(source.Entities.Remove(image)); Assert.Null(source.GetCadObject(handle)); Assert.Null(reactor.Document);
		target.Entities.Add(image); target.UpdateImageReactors();
		Assert.Same(reactor, image.DefinitionReactor); Assert.Same(reactor, target.GetCadObject(reactor.Handle)); Assert.Same(target, reactor.Document);
	}

	[Theory]
	[InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
	public void DxfUnknownReactorKeepsCommonIdentityUnderIndependentReaderFlags(bool keepObject, bool keepEntity)
	{
		var doc = document(ACadVersion.AC1032); var target = doc.RootDictionary.GetEntry<XRecord>("A");
		doc.Entities.First().AddReactor(target);
		doc.Classes.Add(new DxfClass { DxfName = "SYNTHETIC_REACTOR", CppClassName = "SyntheticReactor", ApplicationName = "SYNTHETIC", ItemClassId = 499 });
		var lines = Encoding.UTF8.GetString(write(doc, 1)).Replace("\r\n", "\n").Split('\n');
		var handle = Enumerable.Range(0, lines.Length / 2).Single(i => lines[2 * i].Trim() == "5" && lines[2 * i + 1].Trim() == target.Handle.ToString("X")) * 2;
		Assert.Equal("XRECORD", lines[handle - 1]); lines[handle - 1] = "SYNTHETIC_REACTOR";
		var messages = new List<NotificationEventArgs>(); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
		using var reader = new DxfReader(stream, (_, e) => messages.Add(e));
		reader.Configuration.KeepUnknownNonGraphicalObjects = keepObject; reader.Configuration.KeepUnknownEntities = keepEntity;
		var result = reader.Read(); Assert.DoesNotContain(messages, e => e.NotificationType == NotificationType.Error);
		if (keepObject) Assert.Equal(target.Handle, Assert.IsType<UnknownNonGraphicalObject>(Assert.Single(result.Entities.First().Reactors)).Handle);
		else { Assert.Empty(result.Entities.First().Reactors); Assert.Contains(messages, e => e.Message.Contains("excluded by the reader configuration")); }
	}
	[Theory]
	[InlineData(false)] [InlineData(true)]
	public void PartialReadDiagnosticDoesNotRelaxASubsequentFullRead(bool tables)
	{
		var doc = document(ACadVersion.AC1032); var target = doc.RootDictionary.GetEntry<XRecord>("A");
		doc.Entities.First().AddReactor(target); doc.Layers.AddReactor(target); doc.RootDictionary.AddReactor(target);
		var lines = Encoding.UTF8.GetString(write(doc, 1)).Replace("\r\n", "\n").Split('\n');
		for (int i = 0; i + 3 < lines.Length; i++)
			if (lines[i] == "{ACAD_REACTORS") { Assert.Equal("330", lines[i + 1].Trim()); lines[i + 2] = "FFFFFFF"; }
		var messages = new List<NotificationEventArgs>(); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
		using var reader = new DxfReader(stream, (_, e) => messages.Add(e));
		reader.Configuration.KeepUnknownEntities = true; reader.Configuration.KeepUnknownNonGraphicalObjects = true;
		if (tables) reader.ReadTables(); else reader.ReadEntities();
		Assert.DoesNotContain(messages, e => e.NotificationType == NotificationType.Error);
		Assert.Contains(messages, e => e.NotificationType == NotificationType.Warning && e.Message.Contains("partial read"));
		messages.Clear(); reader.Read();
		Assert.Contains(messages, e => e.NotificationType == NotificationType.Error && e.Message == "Reactor with handle 268435455 not found");
	}
	private static CadDocument imagePair(ACadVersion version)
	{
		var doc = new CadDocument(version);
		foreach (var name in new[] { "FIRST", "SECOND" })
		{
			var image = new RasterImage(new ImageDefinition(name) { FileName = "synthetic.png", Size = new XY(10, 20) }) { Size = new XY(10, 20) };
			image.ClipBoundaryVertices.Add(new XY(-0.5, -0.5)); image.ClipBoundaryVertices.Add(new XY(9.5, 19.5));
			doc.Entities.Add(image);
		}
		doc.UpdateImageReactors();
		return doc;
	}

	private static int[] dxfValues(string[] lines, ulong handle, string code)
	{
		var start = Enumerable.Range(0, lines.Length / 2).Single(i => lines[2 * i].Trim() == "5" && lines[2 * i + 1].Trim() == handle.ToString("X")) * 2;
		var indices = new List<int>();
		for (int i = start + 2; i + 1 < lines.Length && lines[i].Trim() != "0"; i += 2)
			if (lines[i].Trim() == code) indices.Add(i + 1);
		return indices.ToArray();
	}

	[Theory]
	[InlineData(ACadVersion.AC1018, "shared")] [InlineData(ACadVersion.AC1032, "shared")]
	[InlineData(ACadVersion.AC1018, "common")] [InlineData(ACadVersion.AC1032, "common")]
	[InlineData(ACadVersion.AC1018, "associated")] [InlineData(ACadVersion.AC1032, "associated")]
	[InlineData(ACadVersion.AC1018, "both")] [InlineData(ACadVersion.AC1032, "both")]
	public void DxfImageReactorOwnershipErrorsNeverStealAValidImagesReactor(ACadVersion version, string malformed)
	{
		var source = imagePair(version); var images = source.Entities.OfType<RasterImage>().ToArray();
		var reactors = images.Select(i => i.DefinitionReactor).ToArray();
		var lines = Encoding.UTF8.GetString(write(source, 1)).Replace("\r\n", "\n").Split('\n');
		var owners = dxfValues(lines, reactors[0].Handle, "330"); Assert.Equal(2, owners.Length);
		if (malformed == "shared") lines[dxfValues(lines, images[1].Handle, "360").Single()] = reactors[0].Handle.ToString("X");
		if (malformed == "common" || malformed == "both") lines[owners[0]] = images[1].Handle.ToString("X");
		if (malformed == "associated" || malformed == "both") lines[owners[1]] = images[1].Handle.ToString("X");
		var bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines)); var original = bytes.ToArray();
		var messages = new List<NotificationEventArgs>(); using var input = new MemoryStream(bytes);
		using var reader = new DxfReader(input, (_, e) => messages.Add(e)); reader.Configuration.Failsafe = false;
		var result = reader.Read();
		Assert.Contains(messages, m => m.NotificationType == NotificationType.Error && m.Message.Contains("owner/image"));
		var validIndex = malformed == "shared" ? 0 : 1; var invalidIndex = 1 - validIndex;
		var valid = result.GetCadObject<RasterImage>(images[validIndex].Handle); var invalid = result.GetCadObject<RasterImage>(images[invalidIndex].Handle);
		var retained = valid.DefinitionReactor;
		Assert.Null(invalid.DefinitionReactor); Assert.Equal(reactors[validIndex].Handle, retained.Handle);
		Assert.Same(valid, retained.Owner); Assert.Same(valid, retained.Image); Assert.Same(result, retained.Document);
		Assert.True(result.Entities.Remove(invalid));
		Assert.Same(retained, valid.DefinitionReactor); Assert.Same(retained, result.GetCadObject(retained.Handle));
		Assert.Same(valid, retained.Owner); Assert.Same(valid, retained.Image); Assert.Equal(original, bytes);
		for (int i = 0; i < 2; i++) { Assert.Same(reactors[i], images[i].DefinitionReactor); Assert.Same(images[i], reactors[i].Owner); Assert.Same(images[i], reactors[i].Image); }
	}

	[Theory]
	[InlineData(ACadVersion.AC1018)] [InlineData(ACadVersion.AC1032)]
	public void DwgImageReactorOwnerMismatchIsDiagnosedBeforeAssignment(ACadVersion version)
	{
		var source = imagePair(version); var images = source.Entities.OfType<RasterImage>().ToArray();
		// Deliberately malformed native owner; do not pass through the safe property setter.
		images[0].DefinitionReactor.Owner = images[1];
		var bytes = write(source, 0); var messages = new List<NotificationEventArgs>();
		using var input = new MemoryStream(bytes); using var reader = new DwgReader(input, (_, e) => messages.Add(e)); reader.Configuration.Failsafe = false;
		var result = reader.Read();
		Assert.Contains(messages, m => m.NotificationType == NotificationType.Error && m.Message.Contains("owner/image"));
		Assert.Null(result.GetCadObject<RasterImage>(images[0].Handle).DefinitionReactor);
		var valid = result.GetCadObject<RasterImage>(images[1].Handle);
		Assert.Same(valid, valid.DefinitionReactor.Owner); Assert.Same(valid, valid.DefinitionReactor.Image);
	}

	[Fact]
	public void ImageReactorSetterRejectsAnotherOwnerAtomicallyAndAllowsItsOwnReassignment()
	{
		var doc = imagePair(ACadVersion.AC1032); var images = doc.Entities.OfType<RasterImage>().ToArray();
		var first = images[0].DefinitionReactor; var second = images[1].DefinitionReactor;
		Assert.Throws<ArgumentException>(() => images[1].DefinitionReactor = first);
		Assert.Same(first, images[0].DefinitionReactor); Assert.Same(second, images[1].DefinitionReactor);
		Assert.Same(images[0], first.Owner); Assert.Same(images[0], first.Image); Assert.Same(doc, first.Document);
		images[0].DefinitionReactor = first; Assert.Same(first, images[0].DefinitionReactor);
		var detached = (ImageDefinitionReactor)first.Clone(); Assert.Null(detached.Owner); Assert.Null(detached.Image); Assert.Null(detached.Document);
		Assert.Equal(first.ClassVersion, detached.ClassVersion); Assert.Same(images[0], first.Image);
		var withForeignImage = (ImageDefinitionReactor)first.Clone(); withForeignImage.Image = images[0];
		Assert.Throws<ArgumentException>(() => images[1].DefinitionReactor = withForeignImage); Assert.Same(second, images[1].DefinitionReactor);
		Assert.Null(withForeignImage.Owner); Assert.Null(withForeignImage.Document); Assert.Same(images[0], withForeignImage.Image);
	}
}
