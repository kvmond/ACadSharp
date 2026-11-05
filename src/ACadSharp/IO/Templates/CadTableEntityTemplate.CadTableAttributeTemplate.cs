using ACadSharp.Entities;

namespace ACadSharp.IO.Templates;

internal partial class CadTableEntityTemplate
{
	internal class CadTableAttributeTemplate : ICadTemplate
	{
		public ulong? AttDefHandle { get; internal set; }

		private TableEntity.TableAttribute _tableAtt;

		public CadTableAttributeTemplate(TableEntity.TableAttribute tableAtt)
		{
			this._tableAtt = tableAtt;
		}

		public void Build(CadDocumentBuilder builder)
		{
			// TableEntity.TableAttribute keeps the value only, so the attribute definition has nowhere to go.
			// This used to throw, which stopped a drawing with a block cell with attributes from opening at all (R2010 and later).
		}
	}
}