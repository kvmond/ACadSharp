using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ACadSharp.Objects;

public partial class TableStyle
{
	private HashSet<TextStyle> _textReferences = new();
	private HashSet<LineType> _lineReferences = new();
	private bool _refreshingReferences;

	private IEnumerable<CellStyle> AllCells => new[] { this.TableCellStyle, this.TitleCellStyle,
		this.HeaderCellStyle, this.DataCellStyle }.Concat(this.CellStyles).Where(c => c != null).Distinct();

	private void CheckCellOwner(CellStyle cell)
	{
		if (cell == null) throw new ArgumentNullException(nameof(cell));
		if (cell.ResourceOwner != null && cell.ResourceOwner != this)
			throw new ArgumentException("A cell style already belongs to another table style. Clone it first.", nameof(cell));
		foreach (var border in cell.Borders) this.CheckBorderOwner(border);
	}

	internal void CheckBorderOwner(CellBorder border)
	{
		if (border == null) throw new ArgumentNullException(nameof(border));
		if (border.ResourceOwner != null && border.ResourceOwner != this)
			throw new ArgumentException("A border already belongs to another table style. Clone it first.", nameof(border));
	}

	private void BindCell(CellStyle cell)
	{
		this.CheckCellOwner(cell);
		cell.ResourceOwner = this;
		foreach (var border in cell.Borders) border.ResourceOwner = this;
	}

	private void UnbindCell(CellStyle cell)
	{
		if (cell == null || this.AllCells.Contains(cell)) return;
		cell.ResourceOwner = null;
		var retained = new HashSet<CellBorder>(this.AllCells.SelectMany(c => c.Borders));
		foreach (var border in cell.Borders)
			if (!retained.Contains(border)) border.ResourceOwner = null;
	}

	private void SetCell(ref CellStyle field, CellStyle value)
	{
		if (ReferenceEquals(field, value)) return;
		this.CheckCellOwner(value);
		var old = field;
		field = value;
		this.BindCell(value);
		this.UnbindCell(old);
		this.RefreshResourceReferences();
	}

	internal void BorderChanged(CellBorder old, CellBorder value)
	{
		value.ResourceOwner = this;
		if (old != null && !this.AllCells.SelectMany(c => c.Borders).Contains(old)) old.ResourceOwner = null;
		this.RefreshResourceReferences();
	}

	private void RemoveResourceReferences()
	{
		foreach (var entry in this._textReferences) this.Document.TextStyles.RemoveReference(entry.Name, this);
		foreach (var entry in this._lineReferences) this.Document.LineTypes.RemoveReference(entry.Name, this);
		this._textReferences.Clear();
		this._lineReferences.Clear();
	}

	// Tables keep one callback per owner/resource, not per cell. Update every cell sharing that resource.
	internal void RefreshResourceReferences()
	{
		if (this.Document == null || this._refreshingReferences) return;
		this._refreshingReferences = true;
		try
		{
			this.RemoveResourceReferences();
			foreach (var group in this.AllCells.Where(c => c.TextStyle != null)
				.GroupBy(c => c.TextStyle.Name, StringComparer.OrdinalIgnoreCase))
			{
				var cells = group.ToArray();
				var entry = cells[0].TextStyle;
				if (entry.Document != null && entry.Document != this.Document) entry = (TextStyle)entry.Clone();
				this._textReferences.Add(this.Document.TextStyles.UpdateReference(this, entry,
					v => { foreach (var cell in cells) cell.SetTextStyleReference(v); }));
			}
			foreach (var group in this.AllCells.SelectMany(c => c.Borders).Distinct().Where(b => b.LineType != null)
				.GroupBy(b => b.LineType.Name, StringComparer.OrdinalIgnoreCase))
			{
				var borders = group.ToArray();
				var entry = borders[0].LineType;
				if (entry.Document != null && entry.Document != this.Document) entry = (LineType)entry.Clone();
				this._lineReferences.Add(this.Document.LineTypes.UpdateReference(this, entry,
					v => { foreach (var border in borders) border.SetLineTypeReference(v); }));
			}
		}
		finally { this._refreshingReferences = false; }
	}

	internal override void AssignDocument(CadDocument doc)
	{
		base.AssignDocument(doc);
		this.RefreshResourceReferences();
	}

	internal override void UnassignDocument()
	{
		this.RemoveResourceReferences();
		foreach (var cell in this.AllCells)
		{
			cell.SetTextStyleReference((TextStyle)cell.TextStyle?.Clone());
			foreach (var border in cell.Borders) border.SetLineTypeReference((LineType)border.LineType?.Clone());
		}
		base.UnassignDocument();
	}

	/// <inheritdoc/>
	public override CadObject Clone()
	{
		var clone = (TableStyle)base.Clone();
		clone._textReferences = new(); clone._lineReferences = new(); clone._refreshingReferences = false;
		var cells = this.AllCells.ToDictionary(c => c, c => (CellStyle)c.Clone());
		clone._tableCellStyle = cells[this.TableCellStyle]; clone._titleCellStyle = cells[this.TitleCellStyle];
		clone._headerCellStyle = cells[this.HeaderCellStyle]; clone._dataCellStyle = cells[this.DataCellStyle];
		clone.CellStyles = new OwnedCellStyles(clone);
		foreach (var cell in this.CellStyles) clone.CellStyles.Add(cells[cell]);
		foreach (var cell in clone.AllCells) clone.BindCell(cell);
		return clone;
	}

	private sealed class OwnedCellStyles : Collection<CellStyle>
	{
		private readonly TableStyle _owner;
		public OwnedCellStyles(TableStyle owner) { this._owner = owner; }
		protected override void InsertItem(int index, CellStyle item)
		{
			this._owner.CheckCellOwner(item);
			if (this.Contains(item)) throw new ArgumentException("The same cell style is already in the collection.", nameof(item));
			base.InsertItem(index, item); this._owner.BindCell(item); this._owner.RefreshResourceReferences();
		}
		protected override void SetItem(int index, CellStyle item)
		{
			var old = this[index];
			if (ReferenceEquals(old, item)) return;
			this._owner.CheckCellOwner(item);
			if (this.Contains(item)) throw new ArgumentException("The same cell style is already in the collection.", nameof(item));
			base.SetItem(index, item); this._owner.BindCell(item); this._owner.UnbindCell(old); this._owner.RefreshResourceReferences();
		}
		protected override void RemoveItem(int index)
		{
			var old = this[index]; base.RemoveItem(index); this._owner.UnbindCell(old); this._owner.RefreshResourceReferences();
		}
		protected override void ClearItems()
		{
			var old = this.ToArray(); base.ClearItems();
			foreach (var cell in old) this._owner.UnbindCell(cell);
			this._owner.RefreshResourceReferences();
		}
	}
}
