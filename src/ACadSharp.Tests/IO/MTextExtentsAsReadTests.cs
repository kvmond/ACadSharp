using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// An MTEXT's stored extents survive a save (Air CAD W1-20): the extents a DWG file holds outside DXF, while the text and height are unchanged, and
/// from R2018 the extents of the redundant fields (DXF 42 and 43). The reader dropped both and the writer wrote 0 and the defaults 0.9 and 0.2;
/// AutoCAD computes an MTEXT's extents again but shows an attribute definition's embedded text as stored (P0B2-MULTILINE-ATTRIBUTES: 33.34 x 6.80
/// became 0.9 x 0.2 in its dump of the copy).
/// </summary>
public class MTextExtentsAsReadTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static MText SaveAndRead(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document);
		return DwgReader.Read(new MemoryStream(stream.ToArray())).Entities.OfType<MText>().Single();
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheStoredExtentsAreWrittenBackWhileTheTextIsUnchanged(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		var text = new MText { Value = "ONE\\PTWO", Height = 2.5, InsertPoint = new XYZ(10, 2.5, 0), HorizontalWidth = 33.25, VerticalHeight = 6.75 };
		text.ExtentsAsRead = (6.75, 33.25, text.Value, text.Height);
		document.Entities.Add(text);

		var read = SaveAndRead(document);
		Assert.Equal((6.75, 33.25), (read.ExtentsAsRead.Value.Height, read.ExtentsAsRead.Value.Width));
		if (version >= ACadVersion.AC1032)
		{
			Assert.Equal((33.25, 6.75), (read.HorizontalWidth, read.VerticalHeight));
		}

		// A text changed since reading is written with no extents, as before.
		read.Value = "CHANGED";
		var changed = SaveAndRead(read.Document);
		Assert.Equal((0d, 0d), (changed.ExtentsAsRead.Value.Height, changed.ExtentsAsRead.Value.Width));
	}
}
