using ACadSharp.Attributes;
using ACadSharp.Classes;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ACadSharp.Objects;

/// <summary>
/// Represents a <see cref="TableStyle"/> object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectTableStyle"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.TableStyle"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectTableStyle)]
[DxfSubClass(DxfSubclassMarker.TableStyle)]
public partial class TableStyle : NonGraphicalObject, IDxfClassDefined
{
	/// <summary>
	/// Gets the default TableStyle.
	/// </summary>
	public static TableStyle Default { get { return new TableStyle(DefaultName); } }

	/// <summary>
	/// Gets the collection of cell styles applied to the table entity.
	/// </summary>
	public Collection<CellStyle> CellStyles { get; private set; }

	/// <summary>
	/// Gets the style settings applied to data cells within the table entity.
	/// </summary>
	public CellStyle DataCellStyle
	{
		get => this._dataCellStyle;
		set => this.SetCell(ref this._dataCellStyle, value);
	}
	private CellStyle _dataCellStyle = CellStyle.DefaultDataCellStyle;

	/// <summary>
	/// Table style description.
	/// </summary>
	/// <remarks>
	/// 255 characters maximum.
	/// </remarks>
	[DxfCodeValue(3)]
	public string Description { get; set; }

	/// <summary>
	/// Meaning unknown.
	/// </summary>
	[DxfCodeValue(71)]
	public short Flags { get; set; }

	/// <summary>
	/// Gets or sets the direction in which table rows and columns are arranged.
	/// </summary>
	[DxfCodeValue(70)]
	public TableFlowDirectionType FlowDirection { get; set; } = TableFlowDirectionType.Down;

	/// <summary>
	/// Gets the style settings applied to the header cells of the table.
	/// </summary>
	public CellStyle HeaderCellStyle
	{
		get => this._headerCellStyle;
		set => this.SetCell(ref this._headerCellStyle, value);
	}
	private CellStyle _headerCellStyle = CellStyle.DefaultHeaderCellStyle;

	/// <summary>
	/// Gets or sets the horizontal margin, in drawing units, applied to the content within each cell.
	/// </summary>
	[DxfCodeValue(40)]
	public double HorizontalCellMargin { get; set; } = 0.06d;

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectTableStyle;

	/// <inheritdoc/>
	public override ObjectType ObjectType => ObjectType.UNLISTED;

	/// <inheritdoc/>
	public override string SubclassMarker { get; } = DxfSubclassMarker.TableStyle;

	[DxfCodeValue(281)]
	public bool SuppressHeaderRow { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the title should be suppressed.
	/// </summary>
	[DxfCodeValue(280)]
	public bool SuppressTitle { get; set; }

	public CellStyle TableCellStyle
	{
		get => this._tableCellStyle;
		set => this.SetCell(ref this._tableCellStyle, value);
	}
	private CellStyle _tableCellStyle = CellStyle.DefaultTableCellStyle;

	public CellStyle TitleCellStyle
	{
		get => this._titleCellStyle;
		set => this.SetCell(ref this._titleCellStyle, value);
	}
	private CellStyle _titleCellStyle = CellStyle.DefaultTitleCellStyle;

	/// <summary>
	/// Gets or sets the vertical margin, in drawing units, applied to the content within a cell.
	/// </summary>
	[DxfCodeValue(41)]
	public double VerticalCellMargin { get; set; } = 0.06;

	/// <summary>
	/// Represents the default name used when no specific name is provided.
	/// </summary>
	public const string DefaultName = "Standard";

	// Modern DWG fields whose meanings are not established. Preserve read scalars without inferring defaults.
	internal byte RawHeaderByte { get; set; }
	internal int RawHeaderValue1 { get; set; }
	internal int RawHeaderValue2 { get; set; }
	internal ulong RawHeaderHandle { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableStyle"/> class.
	/// </summary>
	public TableStyle() : this(string.Empty) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableStyle"/> class
	/// and sets the name of this style.
	/// </summary>
	public TableStyle(string name) : base(name)
	{
		this.CellStyles = new OwnedCellStyles(this);
		foreach (var cell in this.AllCells) this.BindCell(cell);
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.TableStyle,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectTableStyle,
			ItemClassId = 499,
			MaintenanceVersion = 0,
			ProxyFlags = (ProxyFlags)4095,
			WasZombie = false,
		};
	}
}