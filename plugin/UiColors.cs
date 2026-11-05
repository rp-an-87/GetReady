using UnityEngine;

namespace GetReady
{
	// The text colours of the windows: the game's warm greys, and the three that say how something stands (done, to do, wrong)
	internal static class UiColors
	{
		internal static readonly Color Heading = new Color(0.88f, 0.85f, 0.74f);
		internal static readonly Color Text = new Color(0.88f, 0.88f, 0.84f);
		internal static readonly Color Dim = new Color(0.65f, 0.66f, 0.63f);
		internal static readonly Color Ok = new Color(0.45f, 0.78f, 0.5f);
		internal static readonly Color Warn = new Color(1f, 0.64f, 0.28f);
		internal static readonly Color Bad = new Color(1f, 0.4f, 0.36f);
	}
}
