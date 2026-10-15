using ACadSharp.Objects;

namespace ACadSharp.IO.Templates
{
	internal class CadPlotSettingsTemplate : CadTemplate<PlotSettings>, ICadPlotSettingsTemplate
	{
		public ulong? PlotViewHandle { get; set; }

		public ulong? VisualStyleHandle { get; set; }

		public CadPlotSettingsTemplate(PlotSettings layout) : base(layout) { }

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);
			CadPlotSettingsLinks.Build(builder, this.CadObject, this);
		}
	}
}
