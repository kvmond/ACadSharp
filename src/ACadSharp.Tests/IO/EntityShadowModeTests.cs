using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// An entity's shadow mode (DXF 284 in AcDbEntity, the shadow flags byte of the common entity data in DWG from AC1021 on) is kept (Air CAD W1-8): the
/// DWG reader dropped the byte and the writer wrote 0, so a saved copy reset every entity to casting and receiving shadows. AC1018 stores none. DXF
/// reads 284 only in AcDbEntity (a table's 284 in AcDbTable is a cell border's) and writes it only when it is not the default, as AutoCAD does.
/// </summary>
public class EntityShadowModeTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1027, ACadVersion.AC1032 };

	private static readonly ShadowMode[] Modes = { ShadowMode.CastsAndReceivesShadows, ShadowMode.CastsShadows, ShadowMode.ReceivesShadows, ShadowMode.IgnoresShadows };

	private static ShadowMode[] Expected(ACadVersion version) => version >= ACadVersion.AC1021 ? Modes : Modes.Select(_ => ShadowMode.CastsAndReceivesShadows).ToArray();

	private static CadDocument Lines(ACadVersion version)
	{
		var document = new CadDocument();
		document.Header.Version = version;
		for (var i = 0; i < Modes.Length; i++)
		{
			document.Entities.Add(new Line(new XYZ(0, i, 0), new XYZ(1, i, 0)) { ShadowMode = Modes[i] });
		}
		return document;
	}

	private static ShadowMode[] ModesOf(CadDocument document) => document.Entities.OfType<Line>().OrderBy(l => l.StartPoint.Y).Select(l => l.ShadowMode).ToArray();

	/// <summary>The lines of a text DXF, each with its line end, and the range of the ENTITIES section's groups.</summary>
	private static (List<string> Lines, int Start, int End) Dxf(CadDocument document)
	{
		using var stream = new MemoryStream();
		DxfWriter.Write(stream, document);
		var lines = Encoding.ASCII.GetString(stream.ToArray()).Split('\n').Select(l => l + "\n").ToList();
		var start = Enumerable.Range(0, lines.Count).First(i => i % 2 == 1 && lines[i].Trim() == "ENTITIES");
		var end = Enumerable.Range(start, lines.Count - start).First(i => i % 2 == 1 && lines[i].Trim() == "ENDSEC");
		return (lines, start, end);
	}

	private static CadDocument Read(List<string> lines) => DxfReader.Read(new MemoryStream(Encoding.ASCII.GetBytes(string.Concat(lines).TrimEnd('\n'))));

	[Theory]
	[MemberData(nameof(Versions))]
	public void AnEntitysShadowModeSurvivesADwgRoundTrip(ACadVersion version)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, Lines(version));
		var read = DwgReader.Read(new MemoryStream(stream.ToArray()));

		Assert.Equal(Expected(version), ModesOf(read));
	}

	[Theory]
	[MemberData(nameof(Versions))]
	public void AnEntitysShadowModeSurvivesADxfRoundTripAndIsWrittenOnlyWhenNotTheDefault(ACadVersion version)
	{
		var (lines, start, end) = Dxf(Lines(version));

		Assert.Equal(Expected(version), ModesOf(Read(lines)));
		var written = Enumerable.Range(start, end - start).Count(i => i % 2 == 0 && lines[i].Trim() == "284");
		Assert.Equal(version >= ACadVersion.AC1021 ? 3 : 0, written);
	}

	/// <summary>A 284 after another subclass marker is not the entity's shadow mode: the line of mode 1 carries its 284 into AcDbLine instead.</summary>
	[Fact]
	public void ADxf284OutsideAcDbEntityIsNotTheShadowMode()
	{
		var (lines, start, end) = Dxf(Lines(ACadVersion.AC1032));
		var at = Enumerable.Range(start, end - start).First(i => i % 2 == 0 && lines[i].Trim() == "284" && lines[i + 1].Trim() == "1");
		var group = lines.GetRange(at, 2);
		lines.RemoveRange(at, 2);
		var line = Enumerable.Range(at, end - at).First(i => i % 2 == 1 && lines[i].Trim() == "AcDbLine");
		lines.InsertRange(line + 1, group);

		Assert.Equal(new[] { ShadowMode.CastsAndReceivesShadows, ShadowMode.CastsAndReceivesShadows, ShadowMode.ReceivesShadows, ShadowMode.IgnoresShadows }, ModesOf(Read(lines)));
	}
}
