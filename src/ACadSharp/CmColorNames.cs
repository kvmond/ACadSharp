namespace ACadSharp
{
	/// <summary>
	/// The names a CMC colour carries in DWG after its value (R2004 and later): the colour's name and the name of its colour book, as a colour taken
	/// from one of AutoCAD's colour books has them. Kept with the colour they name, so a colour set anew drops them.
	/// </summary>
	/// <remarks>
	/// AirCAD W1-6: the DWG reader read both names and dropped them and the writer wrote the colour byte 0, so a layer's colour or a text's
	/// background colour from a colour book kept its RGB and lost its names. In DXF they are group 430, <c>BOOK$COLOR</c>.
	/// </remarks>
	public sealed class CmColorNames
	{
		/// <summary>The colour the names belong to.</summary>
		public Color Color { get; }

		/// <summary>The colour byte: 1 when a colour name follows, 2 when a book name follows.</summary>
		public byte Flags { get; }

		/// <summary>The colour's name, empty when none follows.</summary>
		public string ColorName { get; }

		/// <summary>The colour book's name, empty when none follows.</summary>
		public string BookName { get; }

		/// <summary>Names for <paramref name="color"/>: a colour name and a book name, each empty when the colour has none.</summary>
		public CmColorNames(Color color, string colorName, string bookName)
			: this(color, (byte)((string.IsNullOrEmpty(colorName) ? 0 : 1) | (string.IsNullOrEmpty(bookName) ? 0 : 2)), colorName, bookName)
		{
		}

		internal CmColorNames(Color color, byte flags, string colorName, string bookName)
		{
			this.Color = color;
			this.Flags = flags;
			this.ColorName = colorName ?? string.Empty;
			this.BookName = bookName ?? string.Empty;
		}

		/// <summary><paramref name="names"/> when they name <paramref name="color"/>, else null: the names of a colour since set anew are not written.</summary>
		public static CmColorNames For(CmColorNames names, Color color)
		{
			return names != null && names.Color.Equals(color) ? names : null;
		}

		/// <summary>The names of a DXF group 430, <c>BOOK$COLOR</c>, for <paramref name="color"/>; null for none.</summary>
		public static CmColorNames FromDxf(string value, Color color)
		{
			if (string.IsNullOrEmpty(value))
			{
				return null;
			}

			int split = value.IndexOf('$');
			return split < 0
				? new CmColorNames(color, value, string.Empty)
				: new CmColorNames(color, value.Substring(split + 1), value.Substring(0, split));
		}
	}
}
