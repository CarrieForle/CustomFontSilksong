using System;

namespace CustomFont;

/// <summary>
/// Base exception of the library.
/// </summary>
public class CustomFontException : Exception
{
	public CustomFontException(string message) : base(message)
	{

	}
}