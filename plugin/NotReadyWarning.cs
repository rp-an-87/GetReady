using System;
using EFT.UI;
using EFT.UI.Matchmaker;
using UnityEngine;

namespace GetReady
{
	// The map screen, as a PMC: a red NOT READY bar under the title when the plan has a problem or something to fulfil. Nothing is run from
	// here (the plan is only read); a click goes to the character screen, where the stash is. The plan is only kept up to date while the stash
	// is up, so it is read again when the map screen opens.
	internal static class NotReadyWarning
	{
		private static MatchMakerSelectionLocationScreen _screen;
		private static bool _wasActive;
		private static bool _notReady;

		private static readonly Color Red = new Color(0.85f, 0.09f, 0.07f);
		private static readonly Color Glow = new Color(0.8f, 0.17f, 0.14f);
		private static readonly Color Ink = new Color(0.3f, 0.02f, 0.02f);
		private static GUIStyle _text;

		internal static void Picked(MatchMakerSelectionLocationScreen screen)
		{
			_screen = screen;
			Check();
		}

		// Reads the plan again and keeps only whether anything is unresolved
		private static void Check()
		{
			_notReady = false;
			if (Plugin.ShowNotReady == null || !Plugin.ShowNotReady.Value || ReadyRun._running || Game.Controller() == null)
			{
				return;
			}

			ReadyPlan.Refresh();
			int toBring, withProblems;
			ReadyPlan.Tally(out toBring, out withProblems);
			_notReady = toBring > 0 || withProblems > 0;
		}

		internal static void Draw()
		{
			bool active = _screen != null && _screen.isActiveAndEnabled;
			if (active && !_wasActive && Event.current.type == EventType.Layout)
			{
				// Back on the screen (from the stash, say): the inventory may have changed
				try
				{
					Check();
				}
				catch (Exception e)
				{
					Plugin.Log.LogError($"Not ready check failed: {e}");
				}
			}

			if (Event.current.type == EventType.Layout)
			{
				_wasActive = active;
			}

			if (!active || !_notReady || MapWarning.IsScav(_screen))
			{
				return;
			}

			var type = Event.current.type;
			if (type != EventType.Layout && type != EventType.Repaint && type != EventType.MouseDown)
			{
				return;
			}

			OverlayFont.Init();
			float row = Screen.height * 0.045f;
			if (_text == null)
			{
				_text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
				_text.normal.textColor = Ink;
			}

			if (OverlayFont.Font != null && _text.font != OverlayFont.Font)
			{
				_text.font = OverlayFont.Font;
			}

			_text.fontSize = Mathf.RoundToInt(row * 0.4f);
			const string label = "NOT READY";
			float width = _text.CalcSize(new GUIContent(label)).x + row * 1.6f;
			float height = row * 1.3f;
			// Under the title and its "PVE ZONE" line, in the middle
			var bar = new Rect((Screen.width - width) / 2f, Screen.height * 0.1f, width, height);

			if (type == EventType.MouseDown && Event.current.button == 0 && bar.Contains(Event.current.mousePosition))
			{
				Event.current.Use();
				Go();
				return;
			}

			if (type != EventType.Repaint)
			{
				return;
			}

			var halo = new Rect(bar.x - bar.width * 0.3f, bar.y - bar.height * 1.1f, bar.width * 1.6f, bar.height * 3.2f);
			float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f);
			var previous = GUI.color;
			GUI.color = new Color(Glow.r, Glow.g, Glow.b, pulse);
			GUI.DrawTexture(halo, ReadyFlash.GlowTexture(), ScaleMode.StretchToFill, true);
			GUI.color = Red;
			GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
			GUI.color = previous;
			GUI.Label(bar, label, _text);
		}

		// The character button of the menu bar (the screen with the stash); if it is not there, back one screen like BACK does
		private static void Go()
		{
			try
			{
				var bar = UnityEngine.Object.FindObjectOfType<MenuTaskBar>();
				if (bar != null && bar._toggleButtons != null && bar._toggleButtons.TryGetValue(EMenuType.Player, out var toggle) && toggle != null && toggle.interactable)
				{
					toggle.isOn = true;
					return;
				}

				_screen?._backButton?.OnClick?.Invoke();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Going to the stash failed: {e}");
			}
		}
	}
}
