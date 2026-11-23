using ACadSharp.Objects.Evaluations;
using System.Collections.Generic;

namespace ACadSharp.IO.Templates;

internal class CadBlockActionTemplate : CadBlockElementTemplate
{
	public BlockAction BlockAction { get { return this.CadObject as BlockAction; } }

	/// <summary>
	/// The handles of the action's selection as read: in order, repetitions kept, and not only entities (AirCAD F-38).
	/// </summary>
	public List<ulong> EntityHandles { get; } = new();

	public CadBlockActionTemplate(BlockAction blockAction)
		: base(blockAction)
	{
	}

	protected override void build(CadDocumentBuilder builder)
	{
		base.build(builder);

		foreach (var handle in this.EntityHandles)
		{
			if (builder.TryGetCadObject(handle, out CadObject cadObject))
			{
				this.BlockAction.Entities.Add(cadObject);
			}
			else
			{
				builder.Notify($"[{this.BlockAction.ToString()}] object with handle {handle} not found.");
			}
		}
	}
}