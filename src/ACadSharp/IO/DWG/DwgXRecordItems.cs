namespace ACadSharp.IO.DWG;

/// <summary>
/// The value types of an XRECORD's data in a DWG file (Air CAD W1-19). They follow the DXF table of <see cref="GroupCodeValue"/> but for the groups
/// a DXF file splits and a DWG file does not: 38 and 39 (elevation, thickness) are single reals, and 110 to 112 (a UCS's origin and axes) and 210
/// (the extrusion) are points of three reals. AutoCAD 2025's dump and DXF output of the samples show these values, and its files store 8 and 24
/// bytes for them; read with the DXF table, every item after the first such group was read from the wrong place.
/// </summary>
internal static class DwgXRecordItems
{
	/// <summary>
	/// The item code of an XRECORD before R2007 that stands for its 64-bit items (160 to 169): the RS -9999 with no value. Those formats keep the
	/// items in the ACAD extended data of the object, and the data holds this code where they were.
	/// </summary>
	public const short Int64Placeholder = -9999;

	public static GroupCodeValueType TypeOf(int code) => code switch
	{
		38 or 39 => GroupCodeValueType.Double,
		>= 110 and <= 112 or 210 => GroupCodeValueType.Point3D,
		_ => GroupCodeValue.TransformValue(code),
	};
}
