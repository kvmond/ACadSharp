using ACadSharp.Attributes;
using ACadSharp.Classes;
using System;
using System.Linq;

namespace ACadSharp.Objects;

/// <summary>
/// Represents a <see cref="CadDictionaryWithDefault"/> object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectDictionaryWithDefault"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.DictionaryWithDefault"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectDictionaryWithDefault)]
[DxfSubClass(DxfSubclassMarker.DictionaryWithDefault)]
public class CadDictionaryWithDefault : CadDictionary, IDxfClassDefined
{
	/// <summary>
	/// Default entry.
	/// </summary>
	[DxfCodeValue(DxfReferenceType.Handle, 340)]
	public CadObject DefaultEntry
	{
		get
		{
			return _defaultEntry;
		}

		// The dictionary owns its entries; changing this pointer must never unregister them.
		set { this._defaultEntry = value; this._unresolvedDefaultEntryHandle = 0; }
	}

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectDictionaryWithDefault;

	/// <inheritdoc/>
	public override ObjectType ObjectType { get { return ObjectType.UNLISTED; } }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.DictionaryWithDefault;

	/// <summary>Default entry handle, including an unresolved input reference.</summary>
	public ulong DefaultEntryHandle { get => _defaultEntry?.Handle ?? _unresolvedDefaultEntryHandle; internal set => _unresolvedDefaultEntryHandle = value; }
	private ulong _unresolvedDefaultEntryHandle;
	private CadObject _defaultEntry;

	public CadDictionaryWithDefault() : base()
	{
	}

	public CadDictionaryWithDefault(string name, CadObject defaultEntry) : base(name)
	{
		this.DefaultEntry = defaultEntry;
	}

	/// <inheritdoc/>
	public override CadObject Clone()
	{
		if ((_defaultEntry == null && DefaultEntryHandle != 0) || (_defaultEntry != null && !this.Any(e => ReferenceEquals(e, _defaultEntry))))
			throw new InvalidOperationException("The default entry must belong to the dictionary before cloning.");
		var clone = (CadDictionaryWithDefault)base.Clone();
		clone._defaultEntry = _defaultEntry == null ? null : clone.GetEntry<NonGraphicalObject>(((NonGraphicalObject)_defaultEntry).Name);
		// Internal owner reactors can follow the cloned dictionary; external reactors remain detached.
		foreach (var entry in this.Where(e => e.Reactors.Any(r => ReferenceEquals(r, this))))
			clone.GetEntry<NonGraphicalObject>(entry.Name).AddReactor(clone);
		return clone;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.DictionaryWithDefault,
			DwgVersion = (ACadVersion)22,
			DxfName = DxfFileToken.ObjectDictionaryWithDefault,
			ItemClassId = 499,
			MaintenanceVersion = 42,
			ProxyFlags = ProxyFlags.R13FormatProxy,
			WasZombie = false,
		};
	}
}