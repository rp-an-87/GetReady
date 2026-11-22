using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace GetReady.Tests
{
	// The plugin keeps state in statics (settings, caches, lists of what a pass has claimed, hooks like Game.InventoryOf that the tests replace
	// with fakes). A test must not depend on what another one left behind, so every static of the plugin is noted before the first test runs
	// and put back after every test: each test starts from the same state, whatever ran before it.
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class IsolateStaticsAttribute : BeforeAfterTestAttribute
	{
		public override void After(MethodInfo methodUnderTest) => TestIsolation.Restore();
	}

	internal static class TestIsolation
	{
		private class Noted
		{
			public FieldInfo Field;
			public object Value;
			public object[] Contents; // of a static collection that is only ever filled, never replaced
		}

		private static readonly List<Noted> All = new List<Noted>();

		[ModuleInitializer]
		internal static void Note()
		{
			foreach (var type in typeof(Plugin).Assembly.GetTypes())
			{
				if (type.IsGenericTypeDefinition || type.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
				{
					continue;
				}

				foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					if (field.IsLiteral || field.GetCustomAttribute<CompilerGeneratedAttribute>() != null || field.Name.StartsWith("<"))
					{
						continue;
					}

					try
					{
						var value = field.GetValue(null);
						var noted = new Noted { Field = field, Value = value };
						if (field.IsInitOnly)
						{
							if (value == null || !IsCollection(value))
							{
								continue;
							}

							noted.Contents = Contents(value);
						}

						All.Add(noted);
					}
					catch (Exception)
					{
						// A type that can't start outside the game has no state a test could change
					}
				}
			}
		}

		private static bool IsCollection(object value) =>
			(value is IList || value is IDictionary || value.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>))) && !(value is Array);

		private static object[] Contents(object collection)
		{
			if (collection is IDictionary dictionary)
			{
				return dictionary.Cast<DictionaryEntry>().Select(entry => (object)new KeyValuePair<object, object>(entry.Key, entry.Value)).ToArray();
			}

			return ((IEnumerable)collection).Cast<object>().ToArray();
		}

		internal static void Restore()
		{
			foreach (var noted in All)
			{
				if (!noted.Field.IsInitOnly)
				{
					noted.Field.SetValue(null, noted.Value);
					continue;
				}

				var collection = noted.Value;
				if (collection is IDictionary dictionary)
				{
					dictionary.Clear();
					foreach (var pair in noted.Contents.Cast<KeyValuePair<object, object>>())
					{
						dictionary.Add(pair.Key, pair.Value);
					}

					continue;
				}

				var type = collection.GetType();
				var clear = type.GetMethod("Clear", Type.EmptyTypes);
				var add = type.GetMethod("Add", type.GetGenericArguments().Length == 1 ? type.GetGenericArguments() : new Type[0]);
				if (clear == null || add == null)
				{
					continue;
				}

				clear.Invoke(collection, null);
				foreach (var item in noted.Contents)
				{
					add.Invoke(collection, new[] { item });
				}
			}
		}
	}

	// To find tests that lean on each other: with the environment variable GETREADY_TEST_SEED set, the classes and the tests inside them run
	// in an order drawn from that number (the same number, the same order); without it the order is xunit's own.
	public class SeededCollectionOrderer : ITestCollectionOrderer
	{
		public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> collections) => Seeded.Shuffle(collections, c => c.DisplayName, 1);
	}

	public class SeededCaseOrderer : ITestCaseOrderer
	{
		public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase => Seeded.Shuffle(testCases, c => c.UniqueID, 2);
	}

	internal static class Seeded
	{
		internal static IEnumerable<T> Shuffle<T>(IEnumerable<T> items, Func<T, string> key, int salt)
		{
			var seedText = Environment.GetEnvironmentVariable("GETREADY_TEST_SEED");
			if (!int.TryParse(seedText, out var seed))
			{
				return items;
			}

			// Each item gets a number that comes from the seed and its own name, so the order does not depend on how xunit hands them over
			return items.OrderBy(item => new Random(unchecked(seed * 31 + salt * 7919 + StableHash(key(item)))).Next()).ToList();
		}

		private static int StableHash(string text)
		{
			unchecked
			{
				int hash = 17;
				foreach (char c in text)
				{
					hash = hash * 31 + c;
				}

				return hash;
			}
		}
	}
}
