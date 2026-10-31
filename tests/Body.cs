using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.LanguageExtensions;
using NSubstitute;
using EFT;
using EFT.InventoryLogic;

namespace GetReady.Tests
{
	// A real inventory (equipment with pockets, backpack, secured container and a stash, as the game puts it together) for the steps that
	// work on the inventory controller, and the moves they ask for, taken down instead of run.
	internal class Body
	{
		internal const string DefaultInventory = "55d7217a4bdc2d86028b456d", PocketsWithSpecialSlots = "627a4e6b255f7527fb05a0f6", PlainPockets = "557ffd194bdc2d28148b457f",
			Wallet = "5783c43d2459774bbe137486", InjectorCase = "619cbf7d23893217ec30b689", Vest = "5648a69d4bdc2ded0b8b457b", Medkit = RealItems.Grizzly;

		internal class Move
		{
			public Item Item;
			public ItemAddress To;
			public int Count;
		}

		public readonly InventoryEquipment Equipment;
		public readonly Stash StashBox;
		public readonly Inventory Inventory;
		// The game's own inventory controller over it (the moves ask it who owns an item); the moves it would send are taken by RunOperation
		public readonly InventoryController Controller;
		public readonly List<Move> Moves = new List<Move>();
		public readonly List<Item> Merges = new List<Item>();
		public readonly List<KeyValuePair<EBoundItem, Item>> Binds = new List<KeyValuePair<EBoundItem, Item>>();
		// The error a move answers with (null: it works), for the tests of what happens when the game refuses
		public Func<Move, string> Refuse = move => null;

		internal Body(string pockets = PocketsWithSpecialSlots)
		{
			Equipment = (InventoryEquipment)RealItems.Any(DefaultInventory);
			StashBox = (Stash)RealItems.Any(Gear.Stash);
			Wear(EquipmentSlot.Pockets, RealItems.Any(pockets));
			Inventory = new Inventory(Equipment, StashBox, null, null, null, null, new Dictionary<EAreaType, CompoundItem>(), new Dictionary<EBoundItem, MongoID>(),
				new Dictionary<MongoID, int>(), new List<MongoID>(), new List<Error>(), false);
			// The controller reads the game's configuration for its raid restrictions; an empty one is there only while it is made
#pragma warning disable SYSLIB0050
			var configuration = (GlobalConfiguration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(GlobalConfiguration));
#pragma warning restore SYSLIB0050
			Singleton<GlobalConfiguration>.Create(configuration);

			var profile = NSubstitute.Substitute.For<IInventoryProfileInfo>();
			profile.InventoryInfo.Returns(Inventory);
			profile.ProfileId.Returns("profile");
			profile.Nickname.Returns("tester");
			try
			{
				Controller = new InventoryController(profile, true);
			}
			finally
			{
				Singleton<GlobalConfiguration>.Release(configuration);
			}

			Game.InventoryOf = controller => Inventory;
			Game.RunOperation = (controller, operation) => Run(operation);
		}

		internal T Wear<T>(EquipmentSlot slot, T item) where T : Item
		{
			// The way the game puts on what a profile has (a slot can be locked against the player)
			Equipment.GetSlot(slot).Add(item);
			return item;
		}

		internal CompoundItem Container(EquipmentSlot slot) => (CompoundItem)Equipment.GetSlot(slot).ContainedItem;

		internal T InStash<T>(T item) where T : Item => item.In(StashBox);

		internal T OnYou<T>(T item) where T : Item => item.In(Container(EquipmentSlot.Pockets));

		private Task<IResult> Run(OperationResult operation)
		{
			var move = operation.Value as MoveResult;
			var split = operation.Value as SplitResult;
			var taken = move != null ? new Move { Item = move.Item, To = move.To, Count = move.Item.StackObjectsCount }
				: split != null ? new Move { Item = split.Item, To = split.To, Count = split.Count } : new Move();
			var bind = operation.Value as BindResult;
			if (bind != null)
			{
				var bindError = Refuse(new Move { Item = bind.Item, Count = 1 });
				if (bindError != null)
				{
					return Task.FromResult<IResult>(new FailedResult(bindError, 0));
				}

				Binds.Add(new KeyValuePair<EBoundItem, Item>(bind.Index, bind.Item));
				bind.Execute();
				return Task.FromResult<IResult>(SuccessfulResult.New);
			}

			var merge = operation.Value as MergeResult;
			var transfer = operation.Value as TransferResult;
			if (transfer != null)
			{
				var transferError = Refuse(new Move { Item = transfer.Item, Count = transfer.Count });
				if (transferError != null)
				{
					return Task.FromResult<IResult>(new FailedResult(transferError, 0));
				}

				Merges.Add(transfer.Item);
				transfer.Execute();
				return Task.FromResult<IResult>(SuccessfulResult.New);
			}

			if (merge != null)
			{
				// Adding to a stack is not a move to a spot: it is taken down apart
				var error2 = Refuse(new Move { Item = merge.Item, Count = merge.Item.StackObjectsCount });
				if (error2 != null)
				{
					return Task.FromResult<IResult>(new FailedResult(error2, 0));
				}

				Merges.Add(merge.Item);
				merge.Execute();
				return Task.FromResult<IResult>(SuccessfulResult.New);
			}

			var error = Refuse(taken);
			if (error != null)
			{
				return Task.FromResult<IResult>(new FailedResult(error, 0));
			}

			Moves.Add(taken);
			// The move happens, so what comes next sees the item where it went
			if (move != null)
			{
				move.Execute();
			}
			else
			{
				split?.Execute();
			}
			return Task.FromResult<IResult>(SuccessfulResult.New);
		}

		// Where the move put the item: the slot's id, or the container's template
		internal static string Where(Move move) => move.To.Container is Slot slot ? slot.ID : move.To.Container.ParentItem.TemplateId.ToString();
	}
}
