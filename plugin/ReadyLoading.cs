using System.Runtime.CompilerServices;
using UnityEngine;

namespace GetReady
{
	// What the loading bar shows and when: no drawing here, so it can be tested. The bar is shown for every run that has something to do (it also says "it is working"):
	// it fades in at once, stays at least MinShown seconds, completes, holds a moment and fades out.
	internal sealed class LoadingModel
	{
		internal const float ShowAfter = 0f;
		internal const float MinShown = 0.7f;
		internal const float Hold = 0.15f;
		internal const float FadeIn = 0.25f;
		internal const float FadeOut = 0.4f;
		// The bar fills this fast at most (fractions per second), so a jump in the plan is a quick slide, not a cut
		internal const float Speed = 1.5f;
		// While the run is going the bar never reaches the end: only the end of the run fills it
		internal const float RunningCap = 0.97f;
		// With the steps shown one after the other: each name stays this long, and the whole row of them is over this long after the bar appears
		internal const float StepTime = 0.25f;
		internal const float MaxQueue = 2f;

		private float _start = -1f;
		private float _end = -1f;
		private float _last;
		private int _total;
		private int _done;
		private bool _sequence;
		// When each step name is due on screen (see Step)
		private readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>> _slots =
			new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>>();

		// The step the run is at now (the last one named)
		internal string Label = "";
		internal float Displayed;

		internal bool Active => _start >= 0f;

		internal bool Finished => _end >= 0f;

		// sequence: show every step's name for a moment, one after the other, even those that took no time. Without it the label is just the step running.
		internal void Begin(float now, int total, bool sequence = false)
		{
			_sequence = sequence;
			_slots.Clear();
			_start = now;
			_end = -1f;
			_last = now;
			_total = total;
			_done = 0;
			Label = "";
			Displayed = 0f;
		}

		// A step name is due when it came, but not before the bar is there and not before the one before it has had its time (StepTime).
		// Names that pile up are squeezed to end within MaxQueue seconds of the bar appearing.
		internal void Step(string label, float now)
		{
			label = label ?? "";
			Label = label;
			if (!_sequence || (_slots.Count > 0 && _slots[_slots.Count - 1].Value == label))
			{
				return;
			}

			float due = Mathf.Max(now, ShownAt);
			if (_slots.Count > 0)
			{
				due = Mathf.Max(due, _slots[_slots.Count - 1].Key + StepTime);
			}

			_slots.Add(new System.Collections.Generic.KeyValuePair<float, string>(Mathf.Min(due, ShownAt + MaxQueue - StepTime), label));
		}

		// The name to show at this moment
		internal string LabelAt(float now)
		{
			// Once the run is over (and the last name has had its time) it says so, so the last step does not look like it took until the bar went
			if (Finished && now >= Mathf.Max(_end, QueueEnd))
			{
				return "Done";
			}

			if (!_sequence)
			{
				return Label;
			}

			string shown = "";
			foreach (var slot in _slots)
			{
				if (slot.Key <= now)
				{
					shown = slot.Value;
				}
			}

			return shown;
		}

		// When the last name has had its time (0 without sequence)
		private float QueueEnd => _sequence && _slots.Count > 0 ? _slots[_slots.Count - 1].Key + StepTime : 0f;

		internal void LineDone() => _done++;

		internal void Finish(float now) => _end = now;

		internal float Target => Finished ? 1f : _total <= 0 ? 0f : Mathf.Min(RunningCap, (float)_done / _total);

		private float ShownAt => _start + ShowAfter;

		private float FadeStart => Mathf.Max(Mathf.Max(_end, ShownAt + MinShown), QueueEnd) + Hold;

		// Move the bar towards where it should be, and say how visible the whole thing is (0 to 1)
		internal float Tick(float now)
		{
			float dt = Mathf.Max(0f, now - _last);
			_last = now;
			Displayed = Mathf.MoveTowards(Displayed, Target, Speed * dt);
			return Alpha(now);
		}

		internal float Alpha(float now)
		{
			if (!Active || now < ShownAt)
			{
				return 0f;
			}

			float alpha = Mathf.Clamp01((now - ShownAt) / FadeIn);
			if (Finished && now >= FadeStart)
			{
				alpha = Mathf.Min(alpha, Mathf.Clamp01(1f - (now - FadeStart) / FadeOut));
			}

			return alpha;
		}

		// Nothing more to draw: the run ended and the fade is done
		internal bool IsOver(float now) => Active && Finished && now >= FadeStart + FadeOut;

		internal void Reset() => _start = -1f;
	}

	// The loading bar while Get ready works: the screen goes a little darker and a thin bar slides across the middle, like the loading line of
	// the game. Drawn from OnGUI on top of everything; F12 turns it off. It does not block the mouse (a run is guarded against a second start).
	internal static class ReadyLoading
	{
		private const float Dim = 0.3f;

		internal static readonly LoadingModel Model = new LoadingModel();

		private static GUIStyle _title, _step;

		// Unity's clock, apart so that the logic around it can run outside the game (in the tests)
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static float Now() => Time.unscaledTime;

		internal static void Begin(int total)
		{
			// Nothing to do (you are ready already): nothing to show
			if (total > 0 && Plugin.ShowLoading != null && Plugin.ShowLoading.Value)
			{
				Model.Begin(Now(), total, Plugin.ShowLoadingSteps == null || Plugin.ShowLoadingSteps.Value);
			}
		}

		internal static void Step(string label)
		{
			if (Model.Active)
			{
				Model.Step(label, Now());
			}
		}

		internal static void LineDone()
		{
			if (Model.Active)
			{
				Model.LineDone();
			}
		}

		internal static void Finish()
		{
			if (Model.Active)
			{
				Model.Finish(Now());
			}
		}

		// Waits until the bar has faded out (a moment, 4 seconds at the most), so what follows a run (the sound, the notification, the READY flash,
		// the list of problems) comes after it and not over it. Nothing to wait for when the bar was not shown.
		internal static async System.Threading.Tasks.Task WhenDone()
		{
			if (!Model.Active)
			{
				return;
			}

			float limit = Now() + 4f;
			while (Model.Active && Now() < limit)
			{
				await System.Threading.Tasks.Task.Yield();
			}
		}

		internal static void Draw()
		{
			if (!Model.Active || Event.current.type != EventType.Repaint)
			{
				return;
			}

			float now = Now();
			float alpha = Model.Tick(now);
			if (Model.IsOver(now))
			{
				Model.Reset();
				return;
			}

			if (alpha <= 0f)
			{
				return;
			}

			if (_title == null)
			{
				_title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, richText = false, wordWrap = false };
				_step = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = false, wordWrap = false };
			}

			OverlayFont.Init();
			if (OverlayFont.Font != null && _title.font != OverlayFont.Font)
			{
				_title.font = OverlayFont.Font;
				_step.font = OverlayFont.Font;
			}

			var previous = GUI.color;
			GUI.color = new Color(0f, 0f, 0f, Dim * alpha);
			GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

			float width = Screen.width * 0.3f;
			float thickness = Mathf.Max(2f, Screen.height * 0.0028f);
			float y = Screen.height * 0.5f;
			var track = new Rect(Screen.width * 0.5f - width / 2f, y - thickness / 2f, width, thickness);
			GUI.color = new Color(1f, 1f, 1f, 0.16f * alpha);
			GUI.DrawTexture(track, Texture2D.whiteTexture);

			var fill = new Rect(track.x, track.y, track.width * Model.Displayed, thickness);
			var pale = new Color(0.93f, 0.9f, 0.8f);
			GUI.color = new Color(pale.r, pale.g, pale.b, alpha);
			GUI.DrawTexture(fill, Texture2D.whiteTexture);

			// A soft glow at the head of the bar
			float glow = thickness * 9f;
			GUI.color = new Color(pale.r, pale.g, pale.b, 0.5f * alpha);
			GUI.DrawTexture(new Rect(fill.xMax - glow, y - glow / 2f, glow * 2f, glow), ReadyFlash.GlowTexture(), ScaleMode.StretchToFill, true);

			_title.fontSize = Mathf.RoundToInt(Screen.height * 0.019f);
			_step.fontSize = Mathf.RoundToInt(Screen.height * 0.014f);
			float gap = Screen.height * 0.012f;
			GUI.color = new Color(1f, 1f, 1f, 0.9f * alpha);
			GUI.Label(new Rect(track.x, track.y - gap - Screen.height * 0.04f, track.width, Screen.height * 0.04f), "GETTING READY", _title);
			GUI.color = new Color(1f, 1f, 1f, 0.5f * alpha);
			GUI.Label(new Rect(track.x, track.yMax + gap, track.width, Screen.height * 0.03f), (Model.LabelAt(now) ?? "").ToUpperInvariant(), _step);
			GUI.color = previous;
		}
	}
}
