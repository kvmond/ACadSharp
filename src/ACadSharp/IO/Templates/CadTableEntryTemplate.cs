using ACadSharp.Tables;

namespace ACadSharp.IO.Templates
{
	internal class CadTableEntryTemplate<T> : CadTemplate<T>, ICadTableEntryTemplate
		where T : TableEntry
	{
		public string Type { get { return typeof(T).Name; } }

		public string Name { get { return this.CadObject.Name; } }

		/// <summary>
		/// The record's external reference block handle (DWG), resolved to <see cref="TableEntry.XrefBlock"/>.
		/// </summary>
		public ulong? XrefBlockHandle { get; set; }

		public CadTableEntryTemplate(T entry) : base(entry) { }

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);

			if (builder.TryGetCadObject(this.XrefBlockHandle, out BlockRecord xrefBlock))
			{
				this.CadObject.XrefBlock = xrefBlock;
			}
		}
	}
}
