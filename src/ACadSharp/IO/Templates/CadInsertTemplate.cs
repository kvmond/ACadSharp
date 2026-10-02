using ACadSharp.Entities;
using ACadSharp.Tables;
using System.Collections.Generic;

namespace ACadSharp.IO.Templates
{
	internal class CadInsertTemplate : CadEntityTemplate, ICadOwnerTemplate
	{
		public bool HasAtts { get; set; }

		public int OwnedObjectsCount { get; set; }

		public ulong? BlockHeaderHandle { get; set; }

		public string BlockName { get; set; }

		public ulong? FirstAttributeHandle { get; set; }

		public ulong? EndAttributeHandle { get; set; }

		public ulong? SeqendHandle { get; set; }

		public HashSet<ulong> OwnedObjectsHandlers { get; set; } = new();

		public CadInsertTemplate() : base(new Insert()) { }

		public CadInsertTemplate(Insert insert) : base(insert) { }

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);

			if (!(this.CadObject is Insert insert))
				return;

			if (this.getTableReference(builder, this.BlockHeaderHandle, this.BlockName, out BlockRecord block))
			{
				insert.Block = block;
			}
			else
			{
				builder.Notify($"Block {this.BlockHeaderHandle} | {this.BlockName} not found for Insert {this.CadObject.Handle}", NotificationType.Warning);
			}

			if (builder.TryGetCadObject(this.SeqendHandle, out Seqend seqend))
			{
				insert.Attributes.Seqend = seqend;
			}
			else
			{
				//A DXF lists the SEQEND after the attributes as one more owned entity, as it does for polylines.
				//It is set before the attributes are added, as above: adding the first attribute registers it.
				foreach (ulong handle in this.OwnedObjectsHandlers)
				{
					if (builder.TryGetCadObject(handle, out Seqend owned))
					{
						insert.Attributes.Seqend = owned;
						break;
					}
				}
			}

			if (this.FirstAttributeHandle.HasValue)
			{
				var attributes = getEntitiesCollection<AttributeEntity>(builder, FirstAttributeHandle.Value, EndAttributeHandle.Value);
				insert.Attributes.AddRange(attributes);
			}
			else
			{
				foreach (ulong handle in this.OwnedObjectsHandlers)
				{
					if (builder.TryGetCadObject(handle, out AttributeEntity att))
					{
						insert.Attributes.Add(att);
					}
				}
			}
		}
	}
}
