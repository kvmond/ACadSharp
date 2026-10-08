using ACadSharp.Objects;

namespace ACadSharp.IO.Templates;

// DXF stores an associated-image 330 separately from the common owner 330.
internal class CadImageDefinitionReactorTemplate : CadNonGraphicalObjectTemplate
{
	public ulong? ImageHandle { get; set; }

	public CadImageDefinitionReactorTemplate() : base(new ImageDefinitionReactor()) { }
}
