using ACadSharp.Objects;
using ACadSharp.Tables;

namespace ACadSharp.IO.Templates
{
	/// <summary>The handles of a plot settings' view and visual style, linked once the document is built (AirCad).</summary>
	internal interface ICadPlotSettingsTemplate
	{
		ulong? PlotViewHandle { get; set; }

		ulong? VisualStyleHandle { get; set; }
	}

	internal static class CadPlotSettingsLinks
	{
		/// <summary>
		/// Links the view by its handle (R2004+ DWG) or, without one, by the name (DXF, older DWG); a linked view gives the
		/// name, so both say the same. The visual style by its handle.
		/// </summary>
		public static void Build(CadDocumentBuilder builder, PlotSettings plot, ICadPlotSettingsTemplate template)
		{
			if (builder.TryGetCadObject(template.PlotViewHandle, out View view))
			{
				plot.PlotView = view;
			}
			else if (!string.IsNullOrEmpty(plot.PlotViewName) && builder.TryGetTableEntry(plot.PlotViewName, out View named))
			{
				plot.PlotView = named;
			}

			if (plot.PlotView != null)
			{
				plot.PlotViewName = plot.PlotView.Name;
			}

			if (builder.TryGetCadObject(template.VisualStyleHandle, out VisualStyle style))
			{
				plot.VisualStyle = style;
			}
		}
	}
}
