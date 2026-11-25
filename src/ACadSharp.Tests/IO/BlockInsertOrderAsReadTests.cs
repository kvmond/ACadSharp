using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// A block record's list of its inserts keeps the order the file listed them in (Air CAD W1-21). The writer listed them in handle order, and
/// AutoCAD's dump of a copy showed that order where the original listed them otherwise (S18E-TEMPLATE, the block S18-TITLE). An insert the file did
/// not list (a new one) comes after them, in handle order.
/// </summary>
public class BlockInsertOrderAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static CadDocument SaveAndRead(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheInsertsAreListedInTheOrderTheFileListedThem(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var block = new BlockRecord("W1_21");
		block.Entities.Add(new Line(XYZ.Zero, new XYZ(1, 0, 0)));
		document.BlockRecords.Add(block);
		for (int i = 0; i < 3; i++)
		{
			document.Entities.Add(new Insert(block) { InsertPoint = new XYZ(i * 10, 0, 0) });
		}

		var read = SaveAndRead(document);
		var record = read.BlockRecords["W1_21"];
		var handles = read.Entities.OfType<Insert>().Select(i => i.Handle).OrderBy(h => h).ToList();
		Assert.Equal(handles, record.InsertOrderAsRead);

		// The file's order is kept through a save, and a new insert comes after the listed ones.
		record.InsertOrderAsRead = [handles[2], handles[0], handles[1]];
		var again = SaveAndRead(read);
		Assert.Equal([handles[2], handles[0], handles[1]], again.BlockRecords["W1_21"].InsertOrderAsRead);

		again.Entities.Add(new Insert(again.BlockRecords["W1_21"]));
		var added = SaveAndRead(again).BlockRecords["W1_21"].InsertOrderAsRead;
		Assert.Equal(4, added.Count);
		Assert.Equal([handles[2], handles[0], handles[1]], added.Take(3));
	}
}
