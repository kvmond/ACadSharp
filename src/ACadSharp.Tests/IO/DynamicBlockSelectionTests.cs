using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// AirCAD F-38: what the readers dropped from a dynamic block's actions and visibility parameter. An action's selection (71 and 330) kept only the
/// entities, once each, so a move action lost the grips and parameters AutoCAD lists with them; the DWG reader read the visibility parameter's own
/// 281 over the parameter's 281 (chain actions), and both readers a block element's 98 and 99 over its expression's, so a copy wrote one value
/// twice; a visibility state's two lists (332 and 333) lost a repeated handle. The expected values come from AutoCAD's DXF of the same drawings
/// (<c>samples/dynamic-blocks</c>), read here as plain group codes. Copies are written as DWG only, as AirCAD saves (the library's DXF writer
/// stops at these samples' table style before it reaches the blocks).
/// </summary>
public class DynamicBlockSelectionTests
{
	/// <summary>
	/// The samples whose DXF is the DWG's twin. Not BLOCKPOLARPARAMETER: its DWG holds handles beyond the DXF's last (3A5 against 2B2), and the
	/// library reads neither its polar parameter nor its polar stretch action.
	/// </summary>
	public static TheoryData<string> Samples => new()
	{
		"BLOCKALIGNMENTPARAMETER", "BLOCKBASEPOINTPARAMETER", "BLOCKFLIPPARAMETER", "BLOCKLINEARPARAMETER", "BLOCKLOOKUPPARAMETER",
		"BLOCKPOINTPARAMETER", "BLOCKROTATIONPARAMETER", "BLOCKVISIBILITYPARAMETER", "BLOCKXYPARAMETER",
	};

	[Theory]
	[MemberData(nameof(Samples))]
	public void ActionsKeepAutoCadsSelectionWhenReadAndWritten(string name)
	{
		var expected = DxfActions(name);
		var dwg = DwgReader.Read(SamplePath(name + ".dwg"));

		foreach (var (how, document) in new[]
		{
			("dwg", dwg), ("dxf", DxfReader.Read(SamplePath(name + ".dxf"))), ("dwg written", DwgRoundTrip(dwg)),
		})
		{
			var actions = Expressions<BlockAction>(document).ToDictionary(a => a.Handle);
			Assert.True(expected.Keys.OrderBy(h => h).SequenceEqual(actions.Keys.OrderBy(h => h)), $"{how}: the actions");
			foreach (var (handle, handles) in expected)
			{
				Assert.True(handles.SequenceEqual(actions[handle].Entities.Select(o => o.Handle)), $"{how}: the selection of action {handle:X}");
			}
		}
	}

	[Fact]
	public void TheMoveActionKeepsTheGripAutoCadSelectedWithItsLines()
	{
		// The case AirCAD measured: AutoCAD's move action selects the grip of its point parameter with the two lines.
		var move = Expressions<BlockMoveAction>(DwgReader.Read(SamplePath("BLOCKPOINTPARAMETER.dwg"))).Single();

		Assert.Equal(3, move.Entities.Count);
		Assert.IsType<BlockXYGrip>(move.Entities[0]);
	}

	[Fact]
	public void TheVisibilityParameterKeepsBothOfItsFlags()
	{
		var flags = DxfValues("BLOCKVISIBILITYPARAMETER", "BLOCKVISIBILITYPARAMETER", 281).Single().Value;
		Assert.Equal(2, flags.Count);
		Assert.NotEqual(flags[0], flags[1]); // the sample tells the two apart (0 and 1)
		var dwg = DwgReader.Read(SamplePath("BLOCKVISIBILITYPARAMETER.dwg"));

		foreach (var (how, document) in new[]
		{
			("dwg", dwg), ("dxf", DxfReader.Read(SamplePath("BLOCKVISIBILITYPARAMETER.dxf"))), ("dwg written", DwgRoundTrip(dwg)),
		})
		{
			var parameter = Expressions<BlockVisibilityParameter>(document).Single();
			Assert.True(flags[0] == (parameter.ChainActions ? "1" : "0"), $"{how}: the parameter's 281");
			Assert.True(flags[1] == (parameter.Value281 ? "1" : "0"), $"{how}: the visibility parameter's 281");
		}
	}

	[Fact]
	public void RepeatedHandlesOfAStateAndOfAnActionAreWrittenAndReadAsTheyAre()
	{
		var visibility = DwgReader.Read(SamplePath("BLOCKVISIBILITYPARAMETER.dwg"));
		var parameter = Expressions<BlockVisibilityParameter>(visibility).Single();
		var state = parameter.States.Values.First(s => s.Entities.Count > 0);
		state.Entities.Add(state.Entities[0]);
		state.Expressions.Add(parameter);
		state.Expressions.Add(parameter);

		var point = DwgReader.Read(SamplePath("BLOCKPOINTPARAMETER.dwg"));
		var move = Expressions<BlockMoveAction>(point).Single();
		move.Entities.Add(move.Entities[0]);

		var readState = Expressions<BlockVisibilityParameter>(DwgRoundTrip(visibility)).Single().States[state.Name];
		Assert.Equal(state.Entities.Select(e => e.Handle), readState.Entities.Select(e => e.Handle));
		Assert.Equal(new[] { parameter.Handle, parameter.Handle }, readState.Expressions.Select(e => e.Handle));

		var readMove = Expressions<BlockMoveAction>(DwgRoundTrip(point)).Single();
		Assert.Equal(move.Entities.Select(e => e.Handle), readMove.Entities.Select(e => e.Handle));
	}

	[Theory]
	[MemberData(nameof(Samples))]
	public void AnElementReadsItsOwn98And99AsAutoCadWroteThem(string name)
	{
		// AutoCAD writes a 98 and a 99 in the expression's subclass and again in the element's; the readers used to read the second over the first.
		var expected = DxfObjects(name).Where(o => o.Pairs.Contains((100, "AcDbBlockElement"))).ToDictionary(o => Handle(o.Pairs),
			o => (After(o.Pairs, "AcDbEvalExpr", 98), After(o.Pairs, "AcDbEvalExpr", 99), After(o.Pairs, "AcDbBlockElement", 98), After(o.Pairs, "AcDbBlockElement", 99)));
		Assert.NotEmpty(expected);

		foreach (var (how, document) in new[] { ("dwg", DwgReader.Read(SamplePath(name + ".dwg"))), ("dxf", DxfReader.Read(SamplePath(name + ".dxf"))) })
		{
			// The elements the library puts in a graph (it has no type for some kinds, which it does not read).
			var elements = Expressions<BlockElement>(document).ToDictionary(e => e.Handle);
			Assert.NotEmpty(elements);
			foreach (var e in elements.Values)
			{
				var values = expected[e.Handle];
				Assert.True(values == (e.Value98, e.Value99, e.ElementValue98, e.ElementValue99), $"{how}: {e.Handle:X} {values} read as {(e.Value98, e.Value99, e.ElementValue98, e.ElementValue99)}");
			}
		}
	}

	[Fact]
	public void AnElementWritesItsOwn98And99ApartFromTheExpressions()
	{
		// A drawing AutoCAD 2025 saved as AutoCAD 2004 had 33 and 329 in the expression and 25 and 104 in the element of every parameter, grip
		// and action (AirCAD R7-1 U1); the writers wrote the expression's pair twice.
		var document = DwgReader.Read(SamplePath("BLOCKPOINTPARAMETER.dwg"));
		var elements = Expressions<BlockElement>(document).ToList();
		Assert.NotEmpty(elements);
		foreach (var e in elements)
		{
			e.ElementValue98 = 25;
			e.ElementValue99 = 104;
		}

		var readElements = Expressions<BlockElement>(DwgRoundTrip(document)).ToDictionary(e => e.Handle);
		foreach (var e in elements)
		{
			var r = readElements[e.Handle];
			Assert.True((e.Value98, e.Value99, 25, 104) == (r.Value98, r.Value99, r.ElementValue98, r.ElementValue99), $"{e.Handle:X}");
		}
	}

	/// <summary>The first value of a code after a subclass marker.</summary>
	private static int After(List<(int Code, string Value)> pairs, string subclass, int code)
	{
		int at = pairs.IndexOf((100, subclass));
		return int.Parse(pairs.Skip(at + 1).First(p => p.Code == code).Value);
	}

	private static string SamplePath(string file) => Path.Combine(TestVariables.SamplesFolder, "dynamic-blocks", file);

	private static IEnumerable<T> Expressions<T>(CadDocument document) where T : EvaluationExpression =>
		document.BlockRecords
			.Select(r => r.XDictionary != null && r.XDictionary.TryGetEntry(EvaluationGraph.DictionaryEntryName, out EvaluationGraph graph) ? graph : null)
			.Where(g => g != null)
			.SelectMany(g => g.Nodes.Select(n => n.Expression))
			.OfType<T>();

	private static CadDocument DwgRoundTrip(CadDocument document)
	{
		using var stream = new MemoryStream();
		DwgWriter.Write(stream, document, new DwgWriterConfiguration { WriteDynamicBlockData = true });
		return DwgReader.Read(new MemoryStream(stream.ToArray()));
	}

	/// <summary>Every action of AutoCAD's DXF: its handle and the handles after its 71, in order.</summary>
	private static Dictionary<ulong, List<ulong>> DxfActions(string name)
	{
		var actions = new Dictionary<ulong, List<ulong>>();
		foreach (var (_, pairs) in DxfObjects(name).Where(o => o.Type.EndsWith("ACTION", StringComparison.Ordinal)))
		{
			int at = pairs.FindIndex(p => p.Code == 71);
			int count = int.Parse(pairs[at].Value);
			actions.Add(Handle(pairs), pairs.Skip(at + 1).Take(count).Select(p => Convert.ToUInt64(p.Value, 16)).ToList());
		}

		return actions;
	}

	/// <summary>The values of one group code in every object of one type, by handle.</summary>
	private static Dictionary<ulong, List<string>> DxfValues(string name, string type, int code) =>
		DxfObjects(name).Where(o => o.Type == type)
			.ToDictionary(o => Handle(o.Pairs), o => o.Pairs.Where(p => p.Code == code).Select(p => p.Value).ToList());

	private static ulong Handle(List<(int Code, string Value)> pairs) => Convert.ToUInt64(pairs.First(p => p.Code == 5).Value, 16);

	private static List<(string Type, List<(int Code, string Value)> Pairs)> DxfObjects(string name)
	{
		var lines = File.ReadAllLines(SamplePath(name + ".dxf"));
		var objects = new List<(string, List<(int, string)>)>();
		List<(int, string)> current = null;
		for (int i = 0; i + 1 < lines.Length; i += 2)
		{
			int code = int.Parse(lines[i].Trim());
			string value = lines[i + 1].Trim();
			if (code == 0)
			{
				current = new List<(int, string)>();
				objects.Add((value, current));
			}
			else
			{
				current?.Add((code, value));
			}
		}

		return objects;
	}
}
