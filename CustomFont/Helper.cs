using System.Text;
using TeamCherry.Localization;
using UnityEngine;

namespace CustomFont;

static class Helper
{
	public static string Path(GameObject go)
	{
		var t = go.transform;
		var sb = new StringBuilder(t.name);

		while (t.parent != null)
		{
			t = t.parent;
			sb.Insert(0, $"{t.name}/");
		}

		return sb.ToString();
	}

	public static LocalisedString Localized(string key)
	{
		return new LocalisedString($"Mods.{CustomFontPlugin.Id}", key);
	}

	public static string Localized(string key, params object[] args)
	{
		return string.Format(Language.Get(key, $"Mods.{CustomFontPlugin.Id}"), args);
	}
}