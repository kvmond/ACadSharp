using ACadSharp.Attributes;

namespace ACadSharp.Objects.Evaluations;

[DxfSubClass(DxfSubclassMarker.BlockElement)]
public abstract class BlockElement : EvaluationExpression
{
	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockElement;

	/// <summary>
	/// Block element name.
	/// </summary>
	[DxfCodeValue(300)]
	public string ElementName { get; set; }

	[DxfCodeValue(1071)]
	public int Value1071 { get; set; }

	/// <summary>
	/// The block element's own 98, apart from the evaluation expression's (<see cref="EvaluationExpression.Value98"/>): AutoCAD can write the two
	/// differently (a drawing it saved as AutoCAD 2004 had 33 and 329 in the expression and 25 and 104 in the element; AirCAD F-38).
	/// </summary>
	[DxfCodeValue(98)]
	public int ElementValue98 { get; set; } = 33;

	/// <summary>
	/// The block element's own 99, apart from the evaluation expression's (<see cref="EvaluationExpression.Value99"/>).
	/// </summary>
	[DxfCodeValue(99)]
	public int ElementValue99 { get; set; } = 329;
}
