using System.Collections.Generic;
using ACadSharp.Attributes;
using ACadSharp.Entities;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

[DxfSubClass(DxfSubclassMarker.BlockAction)]
public abstract class BlockAction : BlockElement
{
	/// <summary>
	/// Gets the list of objects affected by this <see cref="BlockAction"/>, in the order of the file and with any repetition kept: the
	/// <see cref="Entity"/> objects of the block and, as AutoCAD writes a move action, grips and parameters of the same evaluation graph.
	/// </summary>
	[DxfCodeValue(DxfReferenceType.Count, 71)]
	[DxfCollectionCodeValue(DxfReferenceType.Handle, 330)]
	public List<CadObject> Entities { get; } = new List<CadObject>();

	/// <summary>
	/// Gets or sets the position of the action label.
	/// </summary>
	[DxfCodeValue(1010, 1020, 1030)]
	public XYZ LabelPosition { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockAction;

	[DxfCodeValue(DxfReferenceType.Count, 70)]
	[DxfCollectionCodeValue(91)]
	public List<int> ParametersIds { get; } = new();
}