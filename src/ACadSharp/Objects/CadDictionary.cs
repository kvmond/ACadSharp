using ACadSharp.Attributes;
using ACadSharp.Extensions;
using CSUtilities.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.Objects;

/// <summary>
/// Represents a <see cref="CadDictionary"/> object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectDictionary"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.Dictionary"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectDictionary)]
[DxfSubClass(DxfSubclassMarker.Dictionary)]
public class CadDictionary : NonGraphicalObject, IObservableCadCollection<NonGraphicalObject>
{
	public event EventHandler<CollectionChangedEventArgs> OnAdd;

	public event EventHandler<CollectionChangedEventArgs> OnRemove;

	/// <summary>
	/// Duplicate record cloning flag (determines how to merge duplicate entries)
	/// </summary>
	[DxfCodeValue(281)]
	public DictionaryCloningFlags ClonningFlags { get; set; }

	/// <summary>
	/// Soft-owner ID/handle to entry object
	/// </summary>
	[DxfCodeValue(350)]
	public ulong[] EntryHandles { get { return this._entries.Values.Select(c => c.Handle).ToArray(); } }

	/// <summary>
	/// Entry names
	/// </summary>
	[DxfCodeValue(3)]
	public string[] EntryNames { get { return this._entries.Keys.ToArray(); } }

	/// <summary>
	/// Indicates that elements of the dictionary are to be treated as hard-owned.
	/// </summary>
	[DxfCodeValue(280)]
	public bool HardOwnerFlag { get; set; } = false;

	/// <summary>
	/// Whether the library made this dictionary for a collection the file lacked (<see cref="CadDocument.UpdateCollectionsOfARead"/>); never for a new
	/// document's dictionaries.
	/// </summary>
	/// <remarks>
	/// AirCAD W1-15: such a dictionary (ACAD_IMAGE_DICT and ACAD_PDFDEFINITIONS in a drawing without images or PDF underlays) was written into every
	/// saved copy. The DWG writer leaves it out while it is empty, unless the header points at it. Adding an entry clears the mark for good: a
	/// dictionary that held something may have been written, and a later save that left it out would lose an object of the file.
	/// </remarks>
	internal bool MadeByLibrary { get; set; }

	/// <summary>
	/// AirCAD W1-16: the key a DWG file gave an entry where it differs from the entry's name, by the entry's handle, with the name the entry had
	/// when read. The library keys its entries by their names, so AutoCAD's anonymous scale-list keys (A0, A1, ...) and a multiline style's
	/// "Standard" beside the style's own "STANDARD" were written as the names.
	/// </summary>
	private Dictionary<ulong, (string Key, string Name)> _keysAsRead;

	/// <summary>AirCAD W1-16: keeps <paramref name="key"/>, the key the file gave <paramref name="entry"/>, when it is not the entry's name.</summary>
	internal void SetKeyAsRead(NonGraphicalObject entry, string key)
	{
		if (string.IsNullOrEmpty(key) || entry.Handle == 0 || key == entry.Name)
		{
			return;
		}

		this._keysAsRead ??= new Dictionary<ulong, (string, string)>();
		this._keysAsRead[entry.Handle] = (key, entry.Name);
	}

	/// <summary>
	/// AirCAD W1-16: the name a DWG writer writes for <paramref name="entry"/>: the key the file gave it while the entry still has the name it was
	/// read with and no other entry has that key as its name; otherwise its name, as before.
	/// </summary>
	internal string WrittenKey(NonGraphicalObject entry)
	{
		if (this._keysAsRead != null && this._keysAsRead.TryGetValue(entry.Handle, out var kept) && kept.Name == entry.Name
			&& ReferenceEquals(entry.Owner, this)
			// the keys compare without case: "Standard" finds the entry itself under "STANDARD", which is no other entry
			&& !(this._entries.TryGetValue(kept.Key, out NonGraphicalObject other) && !ReferenceEquals(other, entry)))
		{
			return kept.Key;
		}

		return entry.Name;
	}

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectDictionary;

	/// <inheritdoc/>
	public override ObjectType ObjectType => ObjectType.DICTIONARY;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.Dictionary;

	/// <summary>
	/// ACAD_COLOR dictionary entry.
	/// </summary>
	public const string AcadColor = "ACAD_COLOR";

	/// <summary>
	/// ACAD_FIELDLIST dictionary entry.
	/// </summary>
	public const string AcadFieldList = "ACAD_FIELDLIST";

	/// <summary>
	/// ACAD_GROUP dictionary entry.
	/// </summary>
	public const string AcadGroup = "ACAD_GROUP";

	/// <summary>
	/// ACAD_IMAGE_DICT dictionary entry.
	/// </summary>
	public const string AcadImageDict = "ACAD_IMAGE_DICT";

	/// <summary>
	/// ACAD_LAYOUT dictionary entry.
	/// </summary>
	public const string AcadLayout = "ACAD_LAYOUT";

	/// <summary>
	/// ACAD_MATERIAL dictionary entry.
	/// </summary>
	public const string AcadMaterial = "ACAD_MATERIAL";

	/// <summary>
	/// ACAD_MLEADERSTYLE dictionary entry.
	/// </summary>
	public const string AcadMLeaderStyle = "ACAD_MLEADERSTYLE";

	/// <summary>
	/// ACAD_MLINESTYLE dictionary entry.
	/// </summary>
	public const string AcadMLineStyle = "ACAD_MLINESTYLE";

	/// <summary>
	/// ACAD_PDFDEFINITIONS dictionary entry.
	/// </summary>
	public const string AcadPdfDefinitions = "ACAD_PDFDEFINITIONS";

	/// <summary>
	/// ACAD_PLOTSETTINGS dictionary entry.
	/// </summary>
	public const string AcadPlotSettings = "ACAD_PLOTSETTINGS";

	/// <summary>
	/// ACAD_PLOTSTYLENAME dictionary entry.
	/// </summary>
	public const string AcadPlotStyleName = "ACAD_PLOTSTYLENAME";

	/// <summary>
	/// scales dictionary
	/// </summary>
	public const string AcadScaleList = "ACAD_SCALELIST";

	/// <summary>
	/// ACAD_SORTENTS dictionary entry.
	/// </summary>
	public const string AcadSortEnts = "ACAD_SORTENTS";

	/// <summary>
	/// ACAD_TABLESTYLE dictionary entry.
	/// </summary>
	public const string AcadTableStyle = "ACAD_TABLESTYLE";

	/// <summary>
	/// ACAD_VISUALSTYLE dictionary entry.
	/// </summary>
	public const string AcadVisualStyle = "ACAD_VISUALSTYLE";

	/// <summary>
	/// ACAD_GEOGRAPHICDATA dictionary entry.
	/// </summary>
	public const string GeographicData = "ACAD_GEOGRAPHICDATA";

	/// <summary>
	/// ROOT dictionary, only used in the top level dictionary.
	/// </summary>
	public const string Root = "ROOT";

	/// <summary>
	/// AcDbVariableDictionary dictionary entry.
	/// </summary>
	public const string VariableDictionary = "AcDbVariableDictionary";

	private Dictionary<string, NonGraphicalObject> _entries = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Default constructor.
	/// </summary>
	public CadDictionary()
	{ }

	/// <summary>
	/// Constructor for a named dictionary.
	/// </summary>
	/// <param name="name">Dictionary name.</param>
	public CadDictionary(string name)
	{
		this.Name = name;
	}

	/// <summary>
	/// Create the default entries for the root dictionary.
	/// </summary>
	public static void CreateDefaultEntries(CadDictionary root)
	{
		root.TryAdd(new CadDictionary(AcadColor));
		root.TryAdd(new CadDictionary(AcadGroup));

		root.TryAdd(new CadDictionary(AcadLayout));

		root.TryAdd(new CadDictionary(AcadMaterial));
		root.TryAdd(new CadDictionary(AcadSortEnts));

		root.TryAdd(new CadDictionary(AcadMLeaderStyle));

		root.TryAdd(new CadDictionary(AcadMLineStyle));

		root.TryAdd(new CadDictionary(AcadTableStyle));
		root.TryAdd(new CadDictionary(AcadPlotSettings));

		//root.TryAdd(new CadDictionaryWithDefault(AcadPlotStyleName, new PlotSettings("Normal")));

		// { AcadPlotStyleName, new CadDictionaryWithDefault() },	//Add default entry "Normal"	PlaceHolder	??

		root.TryAdd(new CadDictionary(VariableDictionary));

		//DictionaryVars Entry DIMASSOC and HIDETEXT ??

		root.TryAdd(new CadDictionary(AcadScaleList));

		root.TryAdd(new CadDictionary(AcadVisualStyle));
		root.TryAdd(new CadDictionary(AcadFieldList));
		root.TryAdd(new CadDictionary(AcadImageDict));

		root.TryAdd(new CadDictionary(AcadMaterial));
	}

	/// <summary>
	/// Creates the root dictionary with the default entries.
	/// </summary>
	/// <returns></returns>
	public static CadDictionary CreateRoot()
	{
		CadDictionary root = new CadDictionary(Root);

		CreateDefaultEntries(root);

		return root;
	}

	/// <summary>
	/// Add a <see cref="NonGraphicalObject"/> to the collection, this method triggers <see cref="OnAdd"/>
	/// </summary>
	/// <param name="key">key for the entry in the dictionary</param>
	/// <param name="value"></param>
	public void Add(string key, NonGraphicalObject value)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(value), $"NonGraphicalObject [{this.GetType().FullName}] must have a name");
		}

		this._entries.Add(key, value);
		value.Owner = this;
		//AirCAD W1-15: once it holds an entry it is written like a dictionary the file held, also when it is emptied again
		this.MadeByLibrary = false;

		value.OnNameChanged += this.onEntryNameChanged;

		OnAdd?.Invoke(this, new CollectionChangedEventArgs(value));
	}

	/// <summary>
	/// Add a <see cref="NonGraphicalObject"/> to the collection, this method triggers <see cref="OnAdd"/>
	/// </summary>
	/// <param name="value">the name of the NonGraphicalObject will be used as a key for the dictionary</param>
	/// <exception cref="ArgumentException"></exception>
	public void Add(NonGraphicalObject value)
	{
		this.Add(value.Name, value);
	}

	/// <summary>
	/// Removes all keys and values from the <see cref="CadDictionary"/>.
	/// </summary>
	public void Clear()
	{
		foreach (var item in this._entries)
		{
			this.Remove(item.Key, out _);
		}
	}

	/// <inheritdoc/>
	public override CadObject Clone()
	{
		CadDictionary clone = (CadDictionary)base.Clone();

		clone.OnAdd = null;
		clone.OnRemove = null;
		clone._keysAsRead = null;

		clone._entries = new Dictionary<string, NonGraphicalObject>();
		foreach (NonGraphicalObject item in this._entries.Values)
		{
			clone.Add(item.CloneTyped());
		}

		return clone;
	}

	/// <summary>
	/// Determines whether the <see cref="CadDictionary"/> contains the specified key.
	/// </summary>
	/// <param name="key">The key to locate in the <see cref="CadDictionary"/></param>
	/// <returns></returns>
	public bool ContainsKey(string key)
	{
		return this._entries.ContainsKey(key);
	}

	/// <summary>
	/// Gets the value associated with the specific key
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="name"></param>
	/// <returns>The value with Type T or null if not found or different type</returns>
	public T GetEntry<T>(string name)
		where T : NonGraphicalObject
	{
		this.TryGetEntry<T>(name, out T value);
		return value;
	}

	/// <inheritdoc/>
	public IEnumerator<NonGraphicalObject> GetEnumerator()
	{
		return this._entries.Values.GetEnumerator();
	}

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this._entries.Values.GetEnumerator();
	}

	/// <summary>
	/// Removes a <see cref="NonGraphicalObject"/> from the collection, this method triggers <see cref="OnRemove"/>
	/// </summary>
	/// <param name="key"></param>
	/// <param name="item"></param>
	/// <returns>true if the element is successfully removed; otherwise, false.</returns>
	public bool Remove(string key, out NonGraphicalObject item)
	{
		if (this._entries.Remove(key, out item))
		{
			this._keysAsRead?.Remove(item.Handle);
			item.Owner = null;
			OnRemove?.Invoke(this, new CollectionChangedEventArgs(item));
			item.OnNameChanged -= this.onEntryNameChanged;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Removes a <see cref="NonGraphicalObject"/> from the collection, this method triggers <see cref="OnRemove"/>
	/// </summary>
	/// <param name="key"></param>
	/// <returns>true if the element is successfully removed; otherwise, false.</returns>
	public bool Remove(string key)
	{
		return this.Remove(key, out _);
	}

	/// <summary>
	/// Tries to add the <see cref="NonGraphicalObject"/> entry using the name as key.
	/// </summary>
	/// <param name="value"></param>
	/// <returns>true if the element is successfully added; otherwise, false.</returns>
	public bool TryAdd(NonGraphicalObject value)
	{
		if (!this._entries.ContainsKey(value.Name))
		{
			this.Add(value.Name, value);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Gets the value associated with the specific key
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="name"></param>
	/// <param name="value"></param>
	/// <returns>true if the value is found or false if not found or different type</returns>
	public bool TryGetEntry<T>(string name, out T value)
		where T : NonGraphicalObject
	{
		if (this._entries.TryGetValue(name, out NonGraphicalObject obj))
		{
			if (obj is T t)
			{
				value = t;
				return true;
			}
		}

		value = null;
		return false;
	}

	private void onEntryNameChanged(object sender, OnNameChangedArgs e)
	{
		var entry = this._entries[e.OldName];
		this._entries.Add(e.NewName, entry);
		this._entries.Remove(e.OldName);
	}

	public CadObject this[string key] { get { return this._entries[key]; } }
}