using CSUtilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp;

internal class CadObjectReferenceHandler<TKey, TValue>
{
	private readonly Dictionary<TKey, Dictionary<CadObject, ReferenceHolder>> _references = new();

	public CadObjectReferenceHandler()
	{
	}

	public void AddReference(TKey key, CadObject owner, Action<TValue> assignTo)
	{
		if (!this._references.TryGetValue(key, out Dictionary<CadObject, ReferenceHolder> holders))
		{
			holders = new Dictionary<CadObject, ReferenceHolder>();
			this._references[key] = holders;
		}

		var holder = new ReferenceHolder(owner, assignTo);
		holders[owner] = holder;
	}

	public void RemoveReference(TKey key, CadObject owner)
	{
		if (this._references.TryGetValue(key, out Dictionary<CadObject, ReferenceHolder> holders))
		{
			holders.Remove(owner);
			if (holders.Count == 0)
			{
				this._references.Remove(key);
			}
		}
	}

	public void ChangeKey(TKey current, TKey newKey)
	{
		if (this._references.Remove(current, out Dictionary<CadObject, ReferenceHolder> holders))
		{
			this._references[newKey] = holders;
		}
	}

	public IEnumerable<CadObject> GetReferences(TKey key)
	{
		if (this._references.TryGetValue(key, out Dictionary<CadObject, ReferenceHolder> holders))
		{
			return holders.Keys;
		}

		return Enumerable.Empty<CadObject>();
	}

	public void RemoveReference(TKey key, TValue value)
	{
		if (this._references.Remove(key, out Dictionary<CadObject, ReferenceHolder> holders))
		{
			foreach (var holder in holders.Values)
			{
				holder.AssignTo(value);
			}
		}
	}

	private sealed class ReferenceHolder
	{
		public Action<TValue> AssignTo { get; }

		public CadObject Owner { get; }

		public ReferenceHolder(CadObject owner, Action<TValue> assignTo)
		{
			this.Owner = owner;
			this.AssignTo = assignTo;
		}
	}
}