using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using System.Collections.Generic;

namespace ACadSharp.IO.Templates
{
	internal class CadLayoutTemplate : CadTemplate<Layout>, ICadPlotSettingsTemplate
	{
		public ulong? PlotViewHandle { get; set; }

		public ulong? VisualStyleHandle { get; set; }

		public ulong? PaperSpaceBlockHandle { get; set; }

		public ulong? ActiveViewportHandle { get; set; }

		public ulong? BaseUcsHandle { get; set; }

		public ulong? NamesUcsHandle { get; set; }

		public ulong? LasActiveViewportHandle { get; set; }

		public HashSet<ulong> ViewportHandles { get; set; } = new();

		public CadLayoutTemplate() : base(new Layout()) { }

		public CadLayoutTemplate(Layout layout) : base(layout) { }

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);

			CadPlotSettingsLinks.Build(builder, this.CadObject, this);

			if (builder.TryGetCadObject(this.PaperSpaceBlockHandle, out BlockRecord record))
			{
				this.CadObject.AssociatedBlock = record;
			}

			// DXF 331: a viewport entity in a paper layout, the *Active VPORT in the model layout (the DXF reader gives the second handle).
			ulong? lastActive = this.ActiveViewportHandle ?? this.LasActiveViewportHandle;
			if (builder.TryGetCadObject(lastActive, out Viewport viewport))
			{
				this.CadObject.LastActiveViewport = viewport;
			}
			else if (builder.TryGetCadObject(lastActive, out VPort vport))
			{
				this.CadObject.LastActiveVPort = vport;
			}

			if (builder.TryGetCadObject(this.BaseUcsHandle, out UCS ucs))
			{
				this.CadObject.BaseUCS = ucs;
			}

			if (builder.TryGetCadObject(this.NamesUcsHandle, out UCS nameducs))
			{
				this.CadObject.UCS = nameducs;
			}

			foreach (var handle in this.ViewportHandles)
			{
				if (builder.TryGetCadObject<Viewport>(handle, out Viewport vp))
				{
					//Is repeated, the viewports are already in the entities list in the BLOCK_RECORD
					// if(this.CadObject.AssociatedBlock.Viewports.Contains(vp))
				}
			}
		}
	}
}
