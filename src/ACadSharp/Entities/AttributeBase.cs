using ACadSharp.Attributes;
using ACadSharp.Extensions;

namespace ACadSharp.Entities;

/// <summary>
/// Common base class for <see cref="AttributeEntity" /> and <see cref="AttributeDefinition" />.
/// </summary>
[DxfSubClass(null, true)]
public abstract class AttributeBase : TextEntity
{
	/// <summary>
	/// Attribute type.
	/// </summary>
	[DxfCodeValue(71)]
	public AttributeType AttributeType { get; set; } = AttributeType.SingleLine;

	/// <summary>
	/// Attribute flags.
	/// </summary>
	[DxfCodeValue(70)]
	public AttributeFlags Flags { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the object is currently locked.
	/// </summary>
	public bool IsLocked { get; set; }

	/// <summary>
	/// Gets or sets the multi-line text content associated with this object.
	/// </summary>
	public MText MText
	{
		get { return this._mText; }
		set
		{
			if (ReferenceEquals(this._mText, value))
				return;

			// An embedded child is not a standalone entity. Never steal a child or table references
			// from another registered entity/document when replacing the content.
			MText text = value?.Document == null ? value : value.CloneTyped();
			if (this.Document != null && this._mText?.Document == this.Document)
				this._mText.UnassignDocument();
			this._mText = text;
			if (this.Document != null)
				this._mText?.AssignDocument(this.Document);
		}
	}

	/// <summary>
	/// Specifies the tag string of the object
	/// </summary>
	/// <value>
	/// Cannot contain spaces (not applied)
	/// </value>
	[DxfCodeValue(2)]
	public string Tag
	{
		get { return this._tag; }
		set
		{
			this._tag = value;
			return;

			//TODO: explore AttributeBase tag constrain
			if (value == null)
				throw new System.ArgumentNullException(nameof(value));

			if (value.Contains(" "))
				throw new System.ArgumentException($"Attribute Tag {value} cannot contain spaces", nameof(value));

			this._tag = value;
		}
	}

	[DxfCodeValue(280)]
	public byte Version { get; set; }

	[DxfCodeValue(74)]
	public override TextVerticalAlignmentType VerticalAlignment { get; set; } = TextVerticalAlignmentType.Baseline;

	private string _tag = string.Empty;

	private MText _mText;

	public AttributeBase() : base()
	{
	}

	/// <inheritdoc/>
	public override CadObject Clone()
	{
		AttributeBase clone = (AttributeBase)base.Clone();
		clone._mText = this.MText?.CloneTyped();
		return clone;
	}

	internal override void AssignDocument(CadDocument doc)
	{
		// A detached attribute may have been given an already registered MTEXT instance.
		if (this._mText?.Document != null)
			this._mText = this._mText.CloneTyped();
		base.AssignDocument(doc);
		// Register the child's table references, without allocating an object-map entry or handle.
		this._mText?.AssignDocument(doc);
	}

	internal override void UnassignDocument()
	{
		this._mText?.UnassignDocument();
		base.UnassignDocument();
	}

	protected void matchAttributeProperties(AttributeBase src)
	{
		this.MatchProperties(src);

		this.Thickness = src.Thickness;
		this.InsertPoint = src.InsertPoint;
		this.Height = src.Height;
		this.Value = src.Value;
		this.Rotation = src.Rotation;
		this.WidthFactor = src.WidthFactor;
		this.ObliqueAngle = src.ObliqueAngle;

		if (this.Style.Document != src.Style.Document)
		{
			this.Style = src.Style.CloneTyped();
		}
		else
		{
			this.Style = src.Style;
		}

		this.Mirror = src.Mirror;
		this.HorizontalAlignment = src.HorizontalAlignment;
		this.AlignmentPoint = src.AlignmentPoint;
		this.Normal = src.Normal;
		this.VerticalAlignment = src.VerticalAlignment;

		this.Version = src.Version;
		this.Tag = src.Tag;
		this.Flags = src.Flags;
		this.AttributeType = src.AttributeType;
		this.IsLocked = src.IsLocked;
		this.MText = src.MText?.CloneTyped();

		this.InsertPoint = src.InsertPoint;
	}
}