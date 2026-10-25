using ACadSharp.Objects;
using System;

namespace ACadSharp.IO.Templates
{
	internal class CadXRecordTemplate : CadTemplate<XRecord>
	{
		// AirCAD F-29: a reference keeps its place among the entries. Its entry is made where it was read and gets its object when the
		// document is built; before, the references were appended after every other entry, so a written copy had them out of order. The
		// library read its own copy back the same, but AutoCAD reads some xrecords by position and crashed opening such a copy (a paper
		// view's ADSK_XREC_VTRVIEWINFO: 290, 292, the layout 330, 294).
		private readonly System.Collections.Generic.List<Tuple<XRecord.Entry, ulong>> _entries = new();

		public CadXRecordTemplate() : base(new XRecord()) { }

		public CadXRecordTemplate(XRecord cadObject) : base(cadObject) { }

		public void AddHandleReference(int code, ulong handle)
		{
			_entries.Add(new Tuple<XRecord.Entry, ulong>(this.CadObject.AddPendingEntry(code), handle));
		}

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);

			foreach (var entry in _entries)
			{
				if (builder.TryGetCadObject<CadObject>(entry.Item2, out CadObject obj))
				{
					entry.Item1.Value = obj;
				}
				else
				{
					// AirCAD F-29: an id no object answers keeps its place as the handle read (a null id is common: AutoCAD writes 0 for "none",
					// as in its layer states), so the entries after it do not move; it used to be removed. The writers write an object id as a
					// null id and a handle as its value; AirCAD refuses to save a non-zero object id that names nothing.
					entry.Item1.Value = entry.Item2;
					if (entry.Item2 != 0)
					{
						builder.Notify($"XRecord reference not found {entry.Item1.Code}|{entry.Item2}", NotificationType.Warning);
					}
				}
			}
		}
	}
}
