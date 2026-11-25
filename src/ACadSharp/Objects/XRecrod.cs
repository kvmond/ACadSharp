using ACadSharp.Attributes;
using System.Collections.Generic;

namespace ACadSharp.Objects;

/// <summary>
/// Represents a <see cref="XRecord"/> object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectXRecord"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.XRecord"/>
/// <br/>
/// <br/>
/// Is not recommended to modify, add or remove entries to an XRecord unless you know what consequences will cause in the document.
/// </remarks>
[DxfName(DxfFileToken.ObjectXRecord)]
[DxfSubClass(DxfSubclassMarker.XRecord)]
public partial class XRecord : NonGraphicalObject
{
	/// <summary>
	/// Duplicate record cloning flag (determines how to merge duplicate entries)
	/// </summary>
	[DxfCodeValue(280)]
	public DictionaryCloningFlags CloningFlags { get; set; }

	//1-369 (except 5 and 105)	These values can be used by an application in any way
	public IEnumerable<Entry> Entries { get { return this._entries; } }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectXRecord;

	/// <inheritdoc/>
	public override ObjectType ObjectType => ObjectType.XRECORD;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.XRecord;

	private List<Entry> _entries = new List<Entry>();

	/// <summary>
	/// Where a DWG file before R2007 held the code that stands for the 64-bit items (Air CAD W1-19): the number of entries before each one, in reading
	/// order. Those items are in the ACAD extended data; the writer puts the code back at the same places when it writes a version before R2007.
	/// </summary>
	internal List<int> Int64Placeholders { get; private set; } = new List<int>();

	/// <inheritdoc/>
	public XRecord() : base()
	{
	}

	/// <inheritdoc/>
	public XRecord(string name) : base(name)
	{
	}

	/// <summary>
	/// Clears all entries from the collection.
	/// </summary>
	public void Clear()
	{
		this._entries.Clear();
		this.Int64Placeholders.Clear();
	}

	/// <inheritdoc/>
	public override CadObject Clone()
	{
		XRecord clone = (XRecord)base.Clone();

		clone._entries = new List<Entry>();
		clone.Int64Placeholders = new List<int>(this.Int64Placeholders);
		foreach (var item in this._entries)
		{
			clone._entries.Add(new Entry(item.Code, item.Value, clone));
		}

		return clone;
	}

	/// <summary>
	/// Adds a new entry with the specified code and value to the collection.
	/// </summary>
	/// <param name="code">The integer code that identifies the entry. The value should be unique within the collection.</param>
	/// <param name="value">The value to associate with the entry. Can be any object, including <see langword="null"/>.</param>
	public void CreateEntry(int code, object value)
	{
		this._entries.Add(new Entry(code, value, this));
	}

	/// <summary>An entry added in reading order whose value (a referenced object) the reader sets when the document is built (AirCAD F-29).</summary>
	internal Entry AddPendingEntry(int code)
	{
		var entry = new Entry(code, null, this);
		this._entries.Add(entry);
		return entry;
	}
}