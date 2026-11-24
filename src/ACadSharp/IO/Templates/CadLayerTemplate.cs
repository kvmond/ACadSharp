using ACadSharp.Objects;
using ACadSharp.Tables;

namespace ACadSharp.IO.Templates
{
	internal class CadLayerTemplate : CadTableEntryTemplate<Layer>
	{
		public ulong PlotStyleHandle { get; set; }

		public ulong MaterialHandle { get; set; }

		public ulong? LineTypeHandle { get; set; }

		public string LineTypeName { get; set; }

		/// <summary>DXF 430 of a layer, <c>BOOK$COLOR</c>: the names of its colour book colour (AirCAD W1-6).</summary>
		public string TrueColorName { get; set; }

		public CadLayerTemplate() : base(new Layer()) { }

		public CadLayerTemplate(Layer entry) : base(entry) { }

		protected override void build(CadDocumentBuilder builder)
		{
			//TODO: finish the build for the layer

			base.build(builder);

			if (this.TrueColorName != null && this.CadObject.Color.IsTrueColor)
			{
				//AirCAD W1-6: the names of the colour read from DXF (a colour book colour is a true colour; a 430 without 420 names nothing)
				this.CadObject.ColorNames = CmColorNames.FromDxf(this.TrueColorName, this.CadObject.Color);
			}

			this.CadObject.PlotStyleName = this.PlotStyleHandle;
			if (this.PlotStyleHandle != 0)
			{
				if (builder.TryGetCadObject(this.PlotStyleHandle, out AcdbPlaceHolder plotStyle)) this.CadObject.PlotStyle = plotStyle;
				else builder.Notify($"Layer plot style {this.PlotStyleHandle:X} could not be resolved.", NotificationType.Warning);
			}

			if (builder.TryGetCadObject(this.MaterialHandle, out Material material))
			{
				//Air CAD W1-4: the material was found and not kept, so a written copy lost it
				this.CadObject.Material = material;
			}
			else
			{
				// builder.Notify($"Linetype with handle {this.LineTypeHandle} could not be found for layer {this.CadObject.Name}", NotificationType.Warning);
			}

			if (this.getTableReference(builder, LineTypeHandle, LineTypeName, out LineType lineType))
			{
				this.CadObject.LineType = lineType;
			}
			else
			{
				builder.Notify($"Linetype with handle {this.LineTypeHandle} could not be found for layer {this.CadObject.Name}", NotificationType.Warning);
			}
		}
	}
}
