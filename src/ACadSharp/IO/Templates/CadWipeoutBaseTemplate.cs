using ACadSharp.Entities;
using ACadSharp.Objects;

namespace ACadSharp.IO.Templates
{
	internal class CadWipeoutBaseTemplate : CadEntityTemplate
	{
		public ulong? ImgDefHandle { get; set; }

		public ulong? ImgReactorHandle { get; set; }

		public CadWipeoutBaseTemplate(CadWipeoutBase image) : base(image) { }

		protected override void build(CadDocumentBuilder builder)
		{
			base.build(builder);

			CadWipeoutBase image = this.CadObject as CadWipeoutBase;

			if (builder.TryGetCadObject(this.ImgDefHandle, out ImageDefinition imgDef))
			{
				image.Definition = imgDef;
			}

			if (builder.TryGetCadObject(this.ImgReactorHandle, out ImageDefinitionReactor imgReactor))
			{
				if (!builder.TryGetObjectTemplate(this.ImgReactorHandle, out CadTemplate reactorTemplate)
					|| reactorTemplate.OwnerHandle != image.Handle
					|| reactorTemplate is CadImageDefinitionReactorTemplate dxf && dxf.ImageHandle != image.Handle
					|| imgReactor.Owner != null && imgReactor.Owner != image
					|| imgReactor.Image != null && imgReactor.Image != image)
				{
					string message = $"Image {image.Handle:X} references reactor {imgReactor.Handle:X} whose owner/image reference does not match.";
					builder.Notify(message, NotificationType.Error, new System.IO.InvalidDataException(message));
					return;
				}
				image.DefinitionReactor = imgReactor;
			}
		}
	}
}
