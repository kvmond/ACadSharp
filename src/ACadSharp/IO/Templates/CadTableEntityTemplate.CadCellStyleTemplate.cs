using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using static ACadSharp.Objects.TableStyle;

namespace ACadSharp.IO.Templates;

internal partial class CadTableStyleTemplate
{
	internal class CadCellStyleTemplate : CellContentFormatTemplate
	{
		public List<Tuple<CellBorder, ulong>> BorderLineTypePairs { get; set; } = new();

		public CellStyle CellStyle { get { return this.Format as CellStyle; } }

		public override void Build(CadDocumentBuilder builder)
		{
			base.Build(builder);
			foreach (var pair in this.BorderLineTypePairs)
			{
				if (pair.Item2 == 0) pair.Item1.LineType = null;
				else if (builder.TryGetCadObject(pair.Item2, out LineType lineType)) pair.Item1.LineType = lineType;
				else builder.Notify($"Border linetype reference {pair.Item2:X} not found.", NotificationType.Warning);
			}
		}

		public CadCellStyleTemplate() : base(new CellStyle())
		{
		}

		public CadCellStyleTemplate(CellStyle style) : base(style)
		{			
		}
	}
}