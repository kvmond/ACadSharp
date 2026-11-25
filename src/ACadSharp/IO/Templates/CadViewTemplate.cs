using ACadSharp.Tables;

namespace ACadSharp.IO.Templates
{
	internal class CadViewTemplate : CadTableEntryTemplate<View>
	{
		public ulong? VisualStyleHandle { get; set; }

		public ulong? NamedUcsHandle { get; set; }

		public ulong? UcsHandle { get; set; }

		public CadViewTemplate() : base(new View()) { }

		public CadViewTemplate(View entry) : base(entry) { }

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);

			//AirCAD W1-23: the UCSs the file named, written back while they stay in the UCS table
			if (builder.TryGetCadObject(this.NamedUcsHandle, out UCS named))
			{
				this.CadObject.NamedUcsAsRead = named;
			}

			if (builder.TryGetCadObject(this.UcsHandle, out UCS baseUcs))
			{
				this.CadObject.BaseUcsAsRead = baseUcs;
			}
		}
	}
}
