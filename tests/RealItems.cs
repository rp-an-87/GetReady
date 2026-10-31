using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EFT.InventoryLogic;
using JsonType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace GetReady.Tests
{
	// Real items for the tests, built from the item database of your SPT install with the game's own template reading. Only the
	// templates asked for are read, one by one (reading the whole database trips over Unity-only types). Nothing of it is kept in the repo.
	internal static class RealItems
	{
		internal const string Grizzly = "590c657e86f77412b013051d", Salewa = "544fb45d4bdc2dee738b4568", Afak = "60098ad7c2240c0fe85c570a",
			Ifak = "590c678286f77426c9660122", Car = "590c661e86f7741e566b646a", Ai2 = "5755356824597772cb798962",
			ArmyBandage = "5751a25924597722c463c472", AsepticBandage = "544fb25a4bdc2dfb738b4567", Cat = "60098af40accd37ef2175f27",
			Esmarch = "5e831507ea0a7c419c2f9bd9", CalokB = "5e8488fa988a8701445df1e4", AluSplint = "5af0454c86f7746bf20992e8",
			ImmobilizingSplint = "544fb3364bdc2d34748b456a", Cms = "5d02778e86f774203e7dedbe", Surv12 = "5d02797c86f774203f38e30a";

		// Grenades, signal cartridges and money
		internal const string M67 = "58d3db5386f77426186285a0", Rgd5 = "5448be9a4bdc2dfd2f8b456a", F1 = "5710c24ad2720bc3458b45a3",
			Vog17 = "5e32f56fcb6d5863cc5e5ee4", Vog25 = "5e340dcdcb6d5863cc5e5efb", Rgn = "617fd91e5539a84ec44ce155", Rgo = "618a431df1eb8e24b8741deb",
			GreenFlare = "6217726288ed9f0845317459", RedFlare = "62178c4d4ecf221597654e3d",
			Roubles = "5449016a4bdc2d6f028b456f", Euros = "569668774bdc2da2298b4568", Dollars = "5696686a4bdc2da3298b456a";

		// The game's own converters, but the Unity-only curves (a bullet's drag...) are left out: creating one needs the engine
		private class WithoutCurves : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);
				if (property.PropertyType == typeof(UnityEngine.AnimationCurve))
				{
					property.Ignored = true;
				}

				return property;
			}
		}

		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { Converters = EFT.EftJsonConverters.Converters, ContractResolver = new WithoutCurves() };

		// The game's localization would load fonts from the engine: with no instance set up it just gives the key back
		static RealItems()
		{
#pragma warning disable SYSLIB0050
			EFT.LocalizationManager._instance = (EFT.LocalizationManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EFT.LocalizationManager));
#pragma warning restore SYSLIB0050
		}

		private static JObject _database;
		private static readonly Dictionary<string, ItemTemplate> Templates = new Dictionary<string, ItemTemplate>();
		private static int _next = 10;

		// Where your SPT is: the same SptDir the plugin builds with
		private static string SptDir()
		{
			var folder = new DirectoryInfo(AppContext.BaseDirectory);
			while (folder != null && !File.Exists(Path.Combine(folder.FullName, "GetReady.csproj.user")))
			{
				folder = folder.Parent;
			}

			if (folder == null)
			{
				throw new InvalidOperationException("GetReady.csproj.user (with SptDir) was not found above the tests");
			}

			return XDocument.Load(Path.Combine(folder.FullName, "GetReady.csproj.user")).Descendants().First(element => element.Name.LocalName == "SptDir").Value;
		}

		private static JObject Database()
		{
			if (_database == null)
			{
				var path = Path.Combine(SptDir(), "SPT_Runtime", "SPT_Data", "database", "templates", "items.json");
				_database = JObject.Parse(File.ReadAllText(path));
			}

			return _database;
		}

		// The same, or null when the template is not in the database (an item of a mod that is not installed)
		internal static Item MedsOrNull(string template) => Database()[template] == null ? null : Meds(template);

		// A real medical item (a kit, a bandage, a splint...) of the template. A new item every time: a test that locks or uses one must
		// not change the next test's.
		internal static Item Meds(string template) => Any(template);

		// Any real item of the template (a grenade, money, a cartridge...), made the way the game makes it: by the constructor of its kind
		internal static Item Any(string template, int stack = 1)
		{
			ItemTemplate itemTemplate;
			if (!Templates.TryGetValue(template, out itemTemplate))
			{
				var entry = Database()[template];
				if (entry == null)
				{
					throw new ArgumentException("No template " + template + " in the item database");
				}

				// The template with its parents (Medical item, Meds, Item...), linked like the game does, so "is it under the medikit branch" works
				var templates = new ItemTemplates();
				for (string id = template; !string.IsNullOrEmpty(id) && Database()[id] != null; id = (string)Database()[id]["_parent"])
				{
					var one = JsonConvert.DeserializeObject<ItemTemplates>(new JObject { { id, Database()[id] } }.ToString(), Settings);
					templates.Add(one.Keys.First(), one.Values.First());
				}

				templates.Init();
				itemTemplate = templates.Values.First(candidate => candidate._id.ToString() == template);
				Templates[template] = itemTemplate;
			}

			var item = JsonTypes.ItemConstructors[itemTemplate.ParentId.Value.ToString()]((_next++).ToString("x24"), itemTemplate);
			item.StackObjectsCount = stack;
			return item;
		}
	}
}
