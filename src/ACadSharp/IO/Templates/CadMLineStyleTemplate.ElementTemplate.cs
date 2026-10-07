using ACadSharp.Objects;
using ACadSharp.Tables;

namespace ACadSharp.IO.Templates
{
	internal partial class CadMLineStyleTemplate
	{
		public class ElementTemplate
		{
			public MLineStyle.Element Element { get; set; }

			public ulong? LineTypeHandle { get; set; }

			public int? LinetypeIndex { get; set; }

			public string LineTypeName { get; set; }

			public ElementTemplate(MLineStyle.Element element)
			{
				this.Element = element;
			}

			public void Build(CadDocumentBuilder builder)
			{
				LineType lt;
				if (builder.TryGetCadObject(this.LineTypeHandle, out lt))
				{
					this.Element.LineType = lt;
				}
				else if (builder.TryGetTableEntry(this.LineTypeName, out lt))
				{
					this.Element.LineType = lt;
				}
				else if (this.LinetypeIndex.HasValue)
				{
					if (this.LinetypeIndex == short.MaxValue)
					{
						if (builder.TryGetTableEntry<LineType>(LineType.ByLayerName, out LineType bylayer))
						{
							this.Element.LineType = bylayer;
						}
					}
					else if (this.LinetypeIndex == (short.MaxValue - 1))
					{
						if (builder.TryGetTableEntry<LineType>(LineType.ByBlockName, out LineType byblock))
						{
							this.Element.LineType = byblock;
						}
					}
					else
					{
						var index = this.LinetypeIndex.Value;
						if (index >= 0 && index < builder.LineTypeEntryHandles.Count
							&& builder.TryGetCadObject(builder.LineTypeEntryHandles[index], out lt)) this.Element.LineType = lt;
						else builder.Notify($"Linetype not assigned, index {LinetypeIndex}", NotificationType.Error);
					}
				}
			}
		}
	}
}
