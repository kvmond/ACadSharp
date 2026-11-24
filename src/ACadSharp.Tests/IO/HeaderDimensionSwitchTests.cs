using System.IO;
using ACadSharp.IO;
using Xunit;

namespace ACadSharp.Tests.IO;

/// <summary>
/// The header's four suppression switches survive a DWG round trip (Air CAD W1-12). From R2000 on the header holds DIMSE1 and DIMSE2 in one place
/// and DIMSD1 and DIMSD2 in another; the library read and wrote the second pair through the extension lines' properties, so DIMSE1 and DIMSE2 took
/// the dimension lines' values and DIMSD1 and DIMSD2 were never read (P2-DIMVARS2: DIMSE1 1, DIMSD2 1 came back as DIMSE1 0, DIMSE2 1).
/// </summary>
public class HeaderDimensionSwitchTests
{
	public static TheoryData<ACadVersion> Versions => new() { ACadVersion.AC1015, ACadVersion.AC1018, ACadVersion.AC1024, ACadVersion.AC1032 };

	[Theory]
	[MemberData(nameof(Versions))]
	public void TheExtensionAndDimensionLineSwitchesSurviveADwgRoundTrip(ACadVersion version)
	{
		foreach (var (se1, se2, sd1, sd2) in new[] { (true, false, false, true), (false, true, true, false), (true, true, false, false), (false, false, true, true) })
		{
			var document = new CadDocument();
			document.Header.Version = version;
			document.Header.DimensionSuppressFirstExtensionLine = se1;
			document.Header.DimensionSuppressSecondExtensionLine = se2;
			document.Header.DimensionSuppressFirstDimensionLine = sd1;
			document.Header.DimensionSuppressSecondDimensionLine = sd2;

			using var stream = new MemoryStream();
			DwgWriter.Write(stream, document);
			var header = DwgReader.Read(new MemoryStream(stream.ToArray())).Header;

			Assert.Equal((se1, se2, sd1, sd2), (header.DimensionSuppressFirstExtensionLine, header.DimensionSuppressSecondExtensionLine,
				header.DimensionSuppressFirstDimensionLine, header.DimensionSuppressSecondDimensionLine));
		}
	}
}
