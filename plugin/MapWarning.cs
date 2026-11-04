using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using SPT.Reflection.Patching;
using UnityEngine;

namespace GetReady
{
	// The map screen tells you when a case you carry is for other maps (see MapTags). The screen reports every map you pick through the
	// setter of SelectedLocation, so that is what is patched; the warning is drawn over the screen like the rest of this plugin's windows.
	internal class MapScreenPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(MatchMakerSelectionLocationScreen).GetProperty(nameof(MatchMakerSelectionLocationScreen.SelectedLocation), BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

		[PatchPostfix]
		private static void Postfix(MatchMakerSelectionLocationScreen __instance)
		{
			try
			{
				MapWarning.Picked(__instance, __instance.SelectedLocation?.Id);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError($"Map warning failed: {e}");
			}
		}
	}

	internal static class MapWarning
	{
		private static readonly FieldInfo SideField = typeof(MatchMakerSelectionLocationScreen).GetField("_chosenSideType", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly FieldInfo ProfileField = typeof(MatchMakerSelectionLocationScreen).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic);

		private static MatchMakerSelectionLocationScreen _screen;
		private static string _map;
		private static float _checkedAt = float.NegativeInfinity;

		internal static void Picked(MatchMakerSelectionLocationScreen screen, string locationId)
		{
			_screen = screen;
			_map = MapTags.MapOf(locationId);
			_checkedAt = float.NegativeInfinity;
		}

		// The ids of what you carry (the profile of the screen: the one you are about to raid with); null for a scav raid, which takes the scav's
		// own kit and nothing from your stash
		private static HashSet<string> CarriedIds()
		{
			if (SideField != null && SideField.GetValue(_screen) is ESideType side && side == ESideType.Savage)
			{
				return null;
			}

			var profile = ProfileField?.GetValue(_screen) as Profile;
			if (profile?.Inventory == null)
			{
				return null;
			}

			return new HashSet<string>(Game.CarriedAndSecured(profile.Inventory).Select(item => item.Id.ToString()));
		}

		// Like the GET READY bar of the deploy screen: a bright red bar with dark red letters
		private static readonly Color Red = new Color(0.85f, 0.09f, 0.07f);
		private static readonly Color Glow = new Color(0.8f, 0.17f, 0.14f);
		private static readonly Color Ink = new Color(0.3f, 0.02f, 0.02f);
		private static GUIStyle _text;
		private static MapTags.Report _report = new MapTags.Report();

		private const string WrongText = "WRONG CONTAINERS FOR THIS MAP!";

		// "MISSING CONTAINERS FOR THIS MAP:" when that is all there is, "MISSING:" after the wrong ones
		private static string MissingText(bool alone) => alone ? "MISSING CONTAINERS FOR THIS MAP:" : "MISSING:";

		internal static void Draw()
		{
			var settings = SettingsStore.Current.Cases;
			if (_screen == null || !_screen.isActiveAndEnabled || !settings.MapWarnings || settings.Cases.Count == 0)
			{
				return;
			}

			// Looked at twice a second, in the layout pass only, so the two passes of a frame draw the same thing
			if (Event.current.type == EventType.Layout && Time.unscaledTime - _checkedAt > 0.5f)
			{
				_checkedAt = Time.unscaledTime;
				var ids = CarriedIds();
				_report = ids == null ? new MapTags.Report() : MapTags.Check(settings.Cases, ids, _map, true);
			}

			if (!_report.Any || (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint))
			{
				return;
			}

			Ui.Init();
			float row = Screen.height * 0.045f;
			if (_text == null)
			{
				_text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, richText = false, wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
				_text.normal.textColor = Ink;
			}

			// The font can turn up after the style is made
			if (Ui.Font != null && _text.font != Ui.Font)
			{
				_text.font = Ui.Font;
			}

			_text.fontSize = Mathf.RoundToInt(row * 0.4f);
			float icon = row - 6f;
			float gap = row * 0.22f;
			bool both = _report.Wrong.Count > 0 && _report.Missing.Count > 0;
			string missingText = MissingText(!both);
			Func<string, float> widthOf = text => _text.CalcSize(new GUIContent(text)).x;
			float content = 0f;
			if (_report.Wrong.Count > 0)
			{
				content += _report.Wrong.Count * (icon + gap) + widthOf(WrongText);
			}

			if (both)
			{
				content += row;
			}

			if (_report.Missing.Count > 0)
			{
				content += widthOf(missingText) + gap + _report.Missing.Count * (icon + gap);
			}

			float width = content + row * 1.6f;
			float height = row + row * 0.3f;
			// Just above the time selection of the screen, in the middle
			var bar = new Rect((Screen.width - width) / 2f, Screen.height * 0.745f - height, width, height);

			if (Event.current.type == EventType.Repaint)
			{
				var halo = new Rect(bar.x - bar.width * 0.3f, bar.y - bar.height * 1.1f, bar.width * 1.6f, bar.height * 3.2f);
				float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f);
				var previous = GUI.color;
				GUI.color = new Color(Glow.r, Glow.g, Glow.b, pulse);
				GUI.DrawTexture(halo, ReadyFlash.GlowTexture(), ScaleMode.StretchToFill, true);
				GUI.color = Red;
				GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
				GUI.color = previous;
			}

			// One line: the icons of the wrong containers and what is wrong, then the missing ones
			GUILayout.BeginArea(bar);
			GUILayout.BeginHorizontal(GUILayout.Height(height));
			GUILayout.FlexibleSpace();
			if (_report.Wrong.Count > 0)
			{
				Icons(_report.Wrong, icon, gap);
				GUILayout.Label(WrongText, _text, GUILayout.Height(height));
			}

			if (both)
			{
				GUILayout.Space(row);
			}

			if (_report.Missing.Count > 0)
			{
				GUILayout.Label(missingText, _text, GUILayout.Height(height));
				GUILayout.Space(gap);
				Icons(_report.Missing, icon, gap);
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}

		// The case icons, each with its tag across the bottom of it, centred on the line
		private static void Icons(List<CaseEntry> entries, float size, float gap)
		{
			foreach (var entry in entries)
			{
				GUILayout.BeginVertical(GUILayout.Width(size));
				GUILayout.FlexibleSpace();
				GetReady.Icons.Slot(entry.CaseTemplate, entry.CaseName, size);
				var rect = GUILayoutUtility.GetLastRect();
				GUILayout.FlexibleSpace();
				GUILayout.EndVertical();
				if (Event.current.type == EventType.Repaint)
				{
					TagChip.DrawOn(rect, entry.Tag, entry.TagColor);
				}

				GUILayout.Space(gap);
			}
		}
	}
}
