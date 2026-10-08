using ACadSharp.Entities;
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

public class DxfNestedObjectBoundaryTests
{
    [Theory]
    [InlineData(ACadVersion.AC1018, false)] [InlineData(ACadVersion.AC1032, false)]
    [InlineData(ACadVersion.AC1018, true)] [InlineData(ACadVersion.AC1032, true)]
    public void NestedTableMarkersCannotConsumeTheFollowingDictionary(ACadVersion version, bool truncated)
    {
        var doc = new CadDocument(version); var placeholder = new XRecord("TABLE"); doc.RootDictionary.Add(placeholder);
        var after = new CadDictionary("AFTER"); after.Add(new XRecord("CHILD")); doc.RootDictionary.Add(after);
        var line = new Line(XYZ.Zero, XYZ.AxisX); doc.Entities.Add(line); line.AddReactor(after);
        using var output = new MemoryStream(); DxfWriter.Write(output, doc);
        var lines = Encoding.UTF8.GetString(output.ToArray()).Replace("\r\n", "\n").Split('\n').ToList();
        var handle = Enumerable.Range(0, lines.Count / 2).Single(i => lines[2 * i].Trim() == "5" && lines[2 * i + 1].Trim() == placeholder.Handle.ToString("X")) * 2;
        int start = handle - 2, end = handle + 2; Assert.Equal("XRECORD", lines[start + 1]);
        while (lines[end].Trim() != "0") end += 2;
        var codes = new List<string> { "0", "TABLECONTENT", "5", placeholder.Handle.ToString("X"), "330", doc.RootDictionary.Handle.ToString("X"),
            "100", "AcDbLinkedData", "100", "AcDbLinkedTableData", "90", "0", "91", "1", "301", "ROW", "1", "LINKEDTABLEDATAROW_BEGIN",
            "300", "CELL", "1", "LINKEDTABLEDATACELL_BEGIN", "95", "0", "301", "CUSTOMDATA", "1", "DATAMAP_BEGIN", "90", "0" };
        if (!truncated) codes.AddRange(new[] { "309", "DATAMAP_END", "309", "LINKEDTABLEDATACELL_END", "1", "FORMATTEDTABLEDATACELL_BEGIN", "309", "FORMATTEDTABLEDATACELL_END",
            "1", "TABLECELL_BEGIN", "309", "TABLECELL_END", "309", "LINKEDTABLEDATAROW_END", "1", "FORMATTEDTABLEDATAROW_BEGIN", "309", "FORMATTEDTABLEDATAROW_END",
            "1", "TABLEROW_BEGIN", "309", "TABLEROW_END", "100", "AcDbFormattedTableData", "100", "AcDbTableContent" });
        lines.RemoveRange(start, end - start); lines.InsertRange(start, codes);
        var messages = new List<NotificationEventArgs>(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        var result = DxfReader.Read(input, (_, e) => messages.Add(e));
        var dictionary = Assert.IsType<CadDictionary>(result.GetCadObject(after.Handle));
        Assert.Same(result.RootDictionary, dictionary.Owner); Assert.Same(result, dictionary.Document);
        Assert.NotNull(dictionary.GetEntry<XRecord>("CHILD")); Assert.Same(dictionary, Assert.Single(result.GetCadObject<Line>(line.Handle).Reactors));
        Assert.DoesNotContain(messages, e => e.NotificationType == NotificationType.Error);
        if (truncated) Assert.Contains(messages, e => e.NotificationType == NotificationType.NotImplemented && e.Message.Contains("not fully interpreted"));
        else Assert.DoesNotContain(messages, e => e.Message.Contains("not fully interpreted"));
    }
}
