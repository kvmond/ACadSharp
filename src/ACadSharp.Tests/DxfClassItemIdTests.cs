using ACadSharp.Classes;
using ACadSharp.Entities;
using System;
using System.Linq;
using Xunit;

namespace ACadSharp.Tests;

/// <summary>
/// The class of every type that defines its own DXF class says what the type is: 498 (0x1F2) for an entity and 499 (0x1F3) for an object (Air CAD
/// W1-5, 3.8.0-aircad.26). The DWG writer builds a file's classes from these when it resets them, and a reader that goes by the classes section
/// reads an object of a class marked 499 without an entity's graphics bit: MULTILEADER was marked 499, and Air CAD's own reader could not read
/// the multileaders of a saved copy (AutoCAD reads them by their type).
/// </summary>
public class DxfClassItemIdTests
{
	[Fact]
	public void AnEntityClassIs498AndAnObjectClass499()
	{
		var types = typeof(CadObject).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && typeof(IDxfClassDefined).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null)
			.OrderBy(t => t.FullName, StringComparer.Ordinal)
			.ToList();
		Assert.Contains(typeof(MultiLeader), types);
		Assert.Contains(typeof(Mesh), types);

		var wrong = types
			.Select(t => (Type: t, Class: ((IDxfClassDefined)Activator.CreateInstance(t)!).GetDxfClass()))
			.Where(c => c.Class.ItemClassId != (typeof(Entity).IsAssignableFrom(c.Type) ? 0x1F2 : 0x1F3)
				|| c.Class.IsAnEntity != typeof(Entity).IsAssignableFrom(c.Type))
			.Select(c => $"{c.Type.Name} {c.Class.DxfName} {c.Class.ItemClassId}")
			.ToList();
		Assert.Empty(wrong);
	}
}
