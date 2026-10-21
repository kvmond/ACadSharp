using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using CSMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ACadSharp.Tests.IO.DWG;

/// <summary>
/// <see cref="DwgReader.ReadObjectTypes"/> (Air CAD F5-3, 3.8.0-aircad.14): the type of an object read at its offset in the object
/// map by the code the object reader uses, so an object the reader did not read can be named. The application parses the texts of the
/// reader's notifications about objects it did not read; the second half of the tests pins them. They patch R2000 files (3.8.0-aircad.14
/// writes R2000 objects at absolute offsets, uncompressed, with the type as a bit short coded in one byte), the way a file is damaged.
/// </summary>
public class DwgObjectTypesTests
{
	private const string MaterialName = "AIR_MATERIAL";

	private const string VariableName = "AIR_VARIABLE";

	private const string LayerName = "AIR_LAYER";

	[Theory]
	[InlineData(ACadVersion.AC1015)]
	[InlineData(ACadVersion.AC1018)]
	[InlineData(ACadVersion.AC1024)]
	[InlineData(ACadVersion.AC1027)]
	[InlineData(ACadVersion.AC1032)]
	public void TheTypeOfEachObjectIsWhatTheReaderRegistersItAs(ACadVersion version)
	{
		byte[] bytes = write(version);
		CadDocument read = DwgReader.Read(new MemoryStream(bytes));

		using DwgReader reader = new DwgReader(new MemoryStream(bytes));
		IReadOnlyDictionary<ulong, long> map = reader.ReadObjectMap();
		IReadOnlyDictionary<ulong, string> types = reader.ReadObjectTypes(map.Keys);

		Assert.Equal(map.Keys.OrderBy(h => h), types.Keys.OrderBy(h => h));
		Assert.DoesNotContain(types.Values, t => t == "?" || t.StartsWith("class ", StringComparison.Ordinal));

		// A fixed type, a symbol table entry, and two class based objects (their names come from the file's classes section).
		Assert.Equal("LINE", types[read.Entities.OfType<Line>().Single().Handle]);
		Assert.Equal("LAYER", types[read.Layers[LayerName].Handle]);
		Assert.Equal("MATERIAL", types[read.Materials[MaterialName].Handle]);
		Assert.Equal("DICTIONARYVAR", types[read.DictionaryVariables[VariableName].Handle]);

		// Every object the reader registered: the name of the fixed type it reads, or the DXF name of its class.
		int checkedObjects = 0;
		foreach (KeyValuePair<ulong, string> item in types)
		{
			if (!read.TryGetCadObject(item.Key, out CadObject cadObject))
			{
				continue;
			}

			string expected = cadObject.ObjectType == ObjectType.UNLISTED ? cadObject.ObjectName : cadObject.ObjectType.ToString();
			Assert.Equal(expected, item.Value);
			checkedObjects++;
		}

		Assert.True(checkedObjects > 50, $"only {checkedObjects} objects were compared");
	}

	[Fact]
	public void AHandleThatIsNotInTheObjectMapIsUnreadable()
	{
		byte[] bytes = write(ACadVersion.AC1032);
		using DwgReader reader = new DwgReader(new MemoryStream(bytes));
		IReadOnlyDictionary<ulong, long> map = reader.ReadObjectMap();
		ulong handle = map.Keys.Max() + 1000;

		IReadOnlyDictionary<ulong, string> types = reader.ReadObjectTypes(new ulong[] { handle, 0 });

		Assert.Equal("?", types[handle]);
		Assert.Equal("?", types[0]);
		Assert.Empty(reader.ReadObjectTypes(Array.Empty<ulong>()));
		Assert.Throws<ArgumentNullException>(() => reader.ReadObjectTypes(null));
	}

	[Theory]
	[InlineData(ACadVersion.AC1015)]
	[InlineData(ACadVersion.AC1032)]
	public void ReadingTypesNotifiesNothingAndLeavesTheReaderFitToRead(ACadVersion version)
	{
		byte[] bytes = write(version);
		CadDocument fresh = DwgReader.Read(new MemoryStream(bytes));
		List<string> notifications = new List<string>();

		using DwgReader reader = new DwgReader(new MemoryStream(bytes), (_, e) => notifications.Add(e.Message));
		IReadOnlyDictionary<ulong, long> map = reader.ReadObjectMap();
		IReadOnlyDictionary<ulong, string> first = reader.ReadObjectTypes(map.Keys);
		IReadOnlyDictionary<ulong, string> second = reader.ReadObjectTypes(map.Keys);

		Assert.Empty(notifications);
		Assert.Equal(first.OrderBy(i => i.Key), second.OrderBy(i => i.Key));

		// The reader's own document is not touched: reading it afterwards gives the document a fresh reader gives.
		CadDocument after = reader.Read();
		Assert.Equal(fresh.Classes.Count, after.Classes.Count);
		Assert.Equal(fresh.Entities.Count, after.Entities.Count);
		Assert.Equal(fresh.Materials.Count(), after.Materials.Count());
	}

	[Fact]
	public void AnObjectTheReaderHasNoReaderForIsNamedByItsTypeAndNotifiedWithTheTypesName()
	{
		byte[] bytes = write(ACadVersion.AC1015);
		(ulong line, long offset) = locate(bytes, "LINE");
		setType(bytes, offset, (int)ObjectType.UNKNOW_3A);

		List<NotificationEventArgs> notifications = readNotifications(bytes);

		Assert.Equal("UNKNOW_3A", typeOf(bytes, line));
		NotificationEventArgs notification = Assert.Single(notifications, n => n.NotificationType == NotificationType.NotImplemented);
		Assert.Equal("Object type not implemented: UNKNOW_3A", notification.Message);
		Assert.Equal("Object type not implemented: " + typeOf(bytes, line), notification.Message);
	}

	[Fact]
	public void AnObjectTheReaderFailsToReadIsNotifiedWithItsHandleInDecimal()
	{
		byte[] bytes = write(ACadVersion.AC1015);

		// A fixed type: the type's name, and the handle in decimal (the reader prints the number, not the hex of the file).
		(ulong line, long lineOffset) = locate(bytes, "LINE");
		byte[] asDictionary = (byte[])bytes.Clone();
		setType(asDictionary, lineOffset, (int)ObjectType.DICTIONARY);
		NotificationEventArgs fixedType = Assert.Single(readNotifications(asDictionary), n => n.NotificationType == NotificationType.Error);
		Assert.Equal("DICTIONARY", typeOf(asDictionary, line));
		Assert.Equal($"Could not read DICTIONARY with handle: {line}", fixedType.Message);
		Assert.True(line >= 10, "a decimal handle that differs from its hex form");

		// A class: the DXF name and class number.
		(ulong material, long materialOffset) = locate(bytes, "MATERIAL");
		byte[] damaged = (byte[])bytes.Clone();
		fill(damaged, materialOffset, 8, 0x00);
		NotificationEventArgs classType = Assert.Single(readNotifications(damaged), n => n.NotificationType == NotificationType.Error);
		Assert.Equal("MATERIAL", typeOf(damaged, material));
		short number = DwgReader.Read(new MemoryStream(bytes)).Classes.GetByName("MATERIAL").ClassNumber;
		Assert.True(number >= 500);
		Assert.Equal($"Could not read MATERIAL number {number} with handle: {material}", classType.Message);
	}

	[Fact]
	public void AClassNumberTheFileDoesNotListIsNamedByNumberAndThereIsNoNotificationAboutIt()
	{
		byte[] bytes = write(ACadVersion.AC1015);
		(ulong circle, long offset) = locate(bytes, "CIRCLE");
		setType(bytes, offset, 0x60);

		List<NotificationEventArgs> notifications = readNotifications(bytes);

		Assert.Equal("class 96", typeOf(bytes, circle));
		// The reader warns "Class number 96 not found in classes dictionary." through the object reader's own event, and nobody
		// listens to that event, so the warning never reaches a notification handler. Pinned on purpose: the application tells
		// such an object (its type is "class N") from the reader's silence, and counts the notification if it ever comes.
		Assert.DoesNotContain(notifications, n => n.Message.Contains("Class number", StringComparison.Ordinal));
		Assert.DoesNotContain(notifications, n => n.NotificationType == NotificationType.NotImplemented);
	}

	[Fact]
	public void AnObjectWithoutDataIsUnreadable()
	{
		byte[] bytes = write(ACadVersion.AC1015);
		(ulong line, long offset) = locate(bytes, "LINE");
		bytes[offset] = 0;
		bytes[offset + 1] = 0;

		Assert.Equal("?", typeOf(bytes, line));
	}

	private static byte[] write(ACadVersion version)
	{
		CadDocument doc = new CadDocument();
		doc.Header.Version = version;
		doc.Entities.Add(new Line(XYZ.Zero, new XYZ(1, 0, 0)));
		doc.Entities.Add(new Circle { Center = XYZ.Zero, Radius = 2 });
		doc.Layers.Add(new Layer(LayerName));
		doc.Materials.Add(new Material(MaterialName));
		doc.DictionaryVariables.Add(new DictionaryVariable(VariableName, "value"));

		MemoryStream stream = new MemoryStream();
		using (DwgWriter writer = new DwgWriter(stream, doc))
		{
			writer.Write();
		}

		return stream.ToArray();
	}

	/// <summary>The first object of the type (by handle) and its offset in the file.</summary>
	private static (ulong Handle, long Offset) locate(byte[] bytes, string type)
	{
		using DwgReader reader = new DwgReader(new MemoryStream(bytes));
		IReadOnlyDictionary<ulong, long> map = reader.ReadObjectMap();
		ulong handle = reader.ReadObjectTypes(map.Keys).Where(t => t.Value == type).Select(t => t.Key).OrderBy(h => h).First();
		return (handle, map[handle]);
	}

	private static string typeOf(byte[] bytes, ulong handle)
	{
		using DwgReader reader = new DwgReader(new MemoryStream(bytes));
		return reader.ReadObjectTypes(new[] { handle })[handle];
	}

	private static List<NotificationEventArgs> readNotifications(byte[] bytes)
	{
		List<NotificationEventArgs> notifications = new List<NotificationEventArgs>();
		DwgReader.Read(new MemoryStream(bytes), (_, e) => notifications.Add(e));
		return notifications;
	}

	/// <summary>
	/// Replaces the object's type. R2000 codes a type from 1 to 255 as the bits 01 and the eight bits of the type, right after the
	/// size (a modular short) of the object: the new type must be in that range too.
	/// </summary>
	private static void setType(byte[] bytes, long offset, int type)
	{
		int at = (int)offset + 2;
		if ((bytes[at - 1] & 0x80) != 0)
		{
			at += 2;
		}

		Assert.Equal(1, bytes[at] >> 6);
		Assert.InRange(type, 1, 255);
		bytes[at] = (byte)((bytes[at] & 0xC0) | (type >> 2));
		bytes[at + 1] = (byte)((bytes[at + 1] & 0x3F) | ((type & 3) << 6));
	}

	/// <summary>Overwrites the object's data from <paramref name="from"/> bytes after its offset to its end.</summary>
	private static void fill(byte[] bytes, long offset, int from, byte value)
	{
		int size = bytes[offset] | (bytes[offset + 1] << 8);
		for (long i = offset + from; i < offset + 2 + size; i++)
		{
			bytes[i] = value;
		}
	}
}
