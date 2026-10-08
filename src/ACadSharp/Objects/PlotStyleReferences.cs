using ACadSharp.Entities;
using ACadSharp.Header;
using ACadSharp.Tables;
using System;
using System.IO;
using System.Linq;

namespace ACadSharp.Objects;

// Plot styles are dictionary-owned placeholders, not per-entity owned objects.
internal static class PlotStyleReferences
{
	internal static CadDictionary Dictionary(CadDocument document) => document?.RootDictionary?.GetEntry<CadDictionary>(CadDictionary.AcadPlotStyleName);

	internal static AcdbPlaceHolder Bind(CadDocument document, AcdbPlaceHolder value, bool rejectMissing = true)
	{
		if (document == null || value == null) return value;
		if (ReferenceEquals(value.Document, document))
		{
			if (rejectMissing) Validate(document, value, value.Handle);
			return value;
		}
		var target = Dictionary(document)?.GetEntry<AcdbPlaceHolder>(value.Name);
		if (target == null)
		{
			// AddCadObject registers before AssignDocument. Preserve an unresolved foreign pointer there;
			// never steal its owner or throw halfway through registration. The writer rejects it.
			if (!rejectMissing) return value;
			throw new InvalidOperationException("A plot-style reference requires an existing entry in the destination plot-style dictionary.");
		}
		try { Validate(document, target, target.Handle); }
		catch (InvalidDataException) when (!rejectMissing) { return value; }
		return target;
	}

	internal static void Validate(CadDocument document, AcdbPlaceHolder value, ulong handle)
	{
		if (handle == 0 && value == null) return;
		var dictionary = Dictionary(document);
		if (value == null || handle == 0 || !ReferenceEquals(value.Document, document)
			|| !ReferenceEquals(document.GetCadObject(handle), value) || !ReferenceEquals(value.Owner, dictionary)
			|| dictionary == null || !dictionary.Any(e => ReferenceEquals(e, value)))
			throw new InvalidDataException("A plot-style reference is unresolved or is not a registered member of ACAD_PLOTSTYLENAME.");
	}

	internal static void Validate(Entity entity, ACadVersion version)
	{
		ValidateMode(entity.PlotStyleType, entity.PlotStyleHandle, version, $"{entity.ObjectName}:{entity.Handle:X}", entity is Seqend);
		Validate(entity.Document, entity.PlotStyle, entity.PlotStyleHandle);
	}

	internal static void Validate(Layer layer, ACadVersion version)
	{
		if (version < ACadVersion.AC1015 && layer.PlotStyleName != 0)
			throw new InvalidDataException("Plot-style references require AutoCAD 2000 or later.");
		Validate(layer.Document, layer.PlotStyle, layer.PlotStyleName);
	}

	internal static void ValidateHeader(CadDocument document)
	{
		var header = document.Header;
		ValidateMode(header.CurrentEntityPlotStyle, header.CurrentEntityPlotStyleHandle, header.Version, "HEADER");
		Validate(document, header.CurrentEntityPlotStyleReference, header.CurrentEntityPlotStyleHandle);
		if (Dictionary(document) is CadDictionaryWithDefault dictionary && dictionary.DefaultEntryHandle != 0)
		{
			if (dictionary.DefaultEntry is not AcdbPlaceHolder entry)
				throw new InvalidDataException("The plot-style dictionary default must be a placeholder.");
			Validate(document, entry, entry.Handle);
		}
	}

	internal static void ValidateDefault(CadDictionaryWithDefault dictionary)
	{
		var entry = dictionary.DefaultEntry;
		if (entry == null && dictionary.DefaultEntryHandle == 0) return;
		if (entry == null || !ReferenceEquals(entry.Owner, dictionary) || !dictionary.Any(e => ReferenceEquals(e, entry))
			|| !ReferenceEquals(entry.Document, dictionary.Document) || dictionary.Document == null
			|| !ReferenceEquals(dictionary.Document.GetCadObject(entry.Handle), entry))
			throw new InvalidDataException("A dictionary default must reference a registered member of that dictionary.");
	}

	private static void ValidateMode(EntityPlotStyleType type, ulong handle, ACadVersion version, string owner, bool allowNullObjectId = false)
	{
		if ((short)type < 0 || (short)type > 3 || (handle != 0 && type != EntityPlotStyleType.ByObjectId)
			|| (type == EntityPlotStyleType.ByObjectId && handle == 0 && !allowNullObjectId))
			throw new InvalidDataException($"The plot-style mode and object reference are inconsistent ({owner}: {(short)type}, {handle:X}).");
		if (version < ACadVersion.AC1015 && type != EntityPlotStyleType.ByLayer)
			throw new InvalidDataException("Plot-style references require AutoCAD 2000 or later.");
	}
}
