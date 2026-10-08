using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ACadSharp.IO.DWG;

/// <summary>The fixed, null-terminated LTYPE text area. Offsets belong to the serialized record, not the model.</summary>
internal static class DwgLineTypeText
{
	internal static byte[] Prepare(IReadOnlyList<LineType.Segment> segments, ACadVersion version, Encoding codePage, out short[] offsets)
	{
		if (segments.Count > byte.MaxValue) throw new InvalidDataException("A DWG linetype can contain at most 255 segments.");
		offsets = segments.Select(s => s.ShapeNumber).ToArray();
		bool unicode = version >= ACadVersion.AC1021;
		if (unicode && !segments.Any(s => s.Flags.HasFlag(LineTypeShapeFlags.Text))) return null;
		var area = new byte[unicode ? 512 : 256];
		int cursor = version <= ACadVersion.AC1014 ? 1 : 0;
		int terminator = unicode ? 2 : 1;
		Encoding encoding = textEncoding(unicode, codePage);
		for (int i = 0; i < segments.Count; i++)
		{
			var segment = segments[i];
			if (!segment.Flags.HasFlag(LineTypeShapeFlags.Text)) continue;
			string text = segment.Text ?? string.Empty;
			if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Linetype text cannot contain an embedded null character.");
			int size = encoding.GetByteCount(text);
			if (size > area.Length - cursor - terminator)
				throw new InvalidDataException($"Linetype text exceeds its {area.Length}-byte DWG text area.");
			offsets[i] = (short)cursor;
			encoding.GetBytes(text, 0, text.Length, area, cursor);
			// Even empty text gets its own zero terminator. Offset 0 can already contain nonempty text.
			cursor += size + terminator;
		}
		return area;
	}

	internal static string Read(byte[] area, int offset, ACadVersion version, Encoding codePage)
	{
		bool unicode = version >= ACadVersion.AC1021;
		int width = unicode ? 2 : 1;
		if (area == null || area.Length != (unicode ? 512 : 256) || offset < 0 || offset >= area.Length
			|| unicode && offset % 2 != 0)
			throw new InvalidDataException("Invalid DWG linetype text area or offset.");
		for (int end = offset; end <= area.Length - width; end += width)
			if (area[end] == 0 && (!unicode || area[end + 1] == 0))
				return textEncoding(unicode, codePage).GetString(area, offset, end - offset);
		throw new InvalidDataException("DWG linetype text has no terminator inside its text area.");
	}

	private static Encoding textEncoding(bool unicode, Encoding codePage)
	{
		if (unicode) return new UnicodeEncoding(false, false, true);
		var encoding = (Encoding)(codePage ?? Encoding.ASCII).Clone();
		encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
		encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
		return encoding;
	}
}