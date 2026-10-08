using ACadSharp.Objects;

namespace ACadSharp.IO.Templates;

internal class CadAnnotScaleObjectContextDataTemplate : CadNonGraphicalObjectTemplate
{
	public ulong? ScaleHandle { get; internal set; }

	public CadAnnotScaleObjectContextDataTemplate(AnnotScaleObjectContextData cadObject)
				: base(cadObject)
	{
	}

	protected override void build(CadDocumentBuilder builder)
	{
		base.build(builder);

		AnnotScaleObjectContextData contextData = (AnnotScaleObjectContextData)this.CadObject;
		if (builder.TryGetCadObject(this.ScaleHandle, out Scale scale))
		{
			contextData.Scale = scale;
		}
		else if (contextData is MTextAttributeObjectContextData)
		{
			const string message = "Multiline attribute context refers to a missing or invalid SCALE.";
			builder.Notify(message, NotificationType.Error, new System.IO.InvalidDataException(message));
		}
	}
}
