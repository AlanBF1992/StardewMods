#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Xna.Framework;

using Leclair.Stardew.Common.Enums;
using Leclair.Stardew.Common.Inventory;
using Leclair.Stardew.Common.Types;

using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.TerrainFeatures;

using StardewModdingAPI;
using Leclair.Stardew.Common.Integrations.StackQuality;

namespace Leclair.Stardew.Common;

public record struct LocatedInventory(object Source, GameLocation? Location);

public static class InventoryHelper {

	private static SQIntegration? intSQ;

	public static void InitializeStackQuality(Mod mod) {
		intSQ ??= new SQIntegration(mod);
	}


	#region Item Creation

	[Obsolete("No longer needed in 1.6")]
	public static string GetItemQualifiedId(Item item) {
		return item.QualifiedItemId;
	}

	[return: MaybeNull]
	public static Item CreateObjectOrRing(int id) {
		throw new NotImplementedException("Update for 1.6");
		/*
		if (!Game1.objectInformation.TryGetValue(id, out string? data) || string.IsNullOrWhiteSpace(data) || id < 0)
			return null;

		string[] parts = data.Split('/');
		if (parts.Length > 3 && parts[3] == "Ring")
			return new Ring(id);

		return new SObject(id, 1);*/
	}

	[return: MaybeNull]
	[Obsolete("Not needed in 1.6")]
	public static Item CreateItemById(string id, int amount, int quality = 0, bool allow_null = false) {
		return ItemRegistry.Create(id, amount, quality, allow_null);
	}

	#endregion

	#region Discovery

	public static List<LocatedInventory> DiscoverInventories(
		Vector2 source,
		GameLocation? location,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit = 5,
		int scanLimit = 100,
		int targetLimit = 20,
		bool includeSource = true,
		bool includeDiagonal = true
	) {
		return DiscoverInventories(
			new AbsolutePosition(location, source),
			who,
			getProvider,
			checkConnector,
			distanceLimit,
			scanLimit,
			targetLimit,
			includeSource,
			includeDiagonal
		);
	}

	public static List<LocatedInventory> DiscoverInventories(
		Rectangle source,
		GameLocation? location,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit = 5,
		int scanLimit = 100,
		int targetLimit = 20,
		bool includeSource = true,
		bool includeDiagonal = true,
		int expandSource = 0
	) {
		List<AbsolutePosition> positions = new();

		for (int x = -expandSource; x < source.Width + expandSource; x++) {
			for (int y = -expandSource; y < source.Height + expandSource; y++) {
				positions.Add(new(
					location,
					new(
						source.X + x,
						source.Y + y
					)
				));
			}
		}

		return DiscoverInventories(
			positions,
			who,
			getProvider,
			checkConnector,
			distanceLimit,
			scanLimit,
			targetLimit,
			includeSource,
			includeDiagonal
		);
	}

	public static List<LocatedInventory> DiscoverInventories(
		AbsolutePosition source,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit = 5,
		int scanLimit = 100,
		int targetLimit = 20,
		bool includeSource = true,
		bool includeDiagonal = true,
		int expandSource = 0
	) {
		List<AbsolutePosition> potentials = new();

		if (expandSource == 0)
			potentials.Add(source);
		else {
			for(int x = -expandSource; x < expandSource; x++) {
				for(int y = -expandSource; y < expandSource; y++) {
					potentials.Add(new(source.Location, new Vector2(source.Position.X + x, source.Position.Y + y)));
				}
			}
		}

		Dictionary<AbsolutePosition, Vector2> origins = new();
		origins[source] = source.Position;

		AddPotentials(source.Position, source.Position, source.Location, potentials, origins, distanceLimit, includeDiagonal);

		return WalkPotentials(
			potentials,
			origins,
			includeSource ? 0 : 1,
			who,
			getProvider,
			checkConnector,
			distanceLimit,
			scanLimit,
			targetLimit,
			includeDiagonal,
			null
		);
	}

	public static void DeduplicateInventories(ref IList<LocatedInventory> inventories) {
		HashSet<object> objects = new();
		for(int i = 0; i < inventories.Count; i++) {
			LocatedInventory inv = inventories[i];
			if (objects.Contains(inv.Source)) {
				inventories.RemoveAt(i);
				i--;
			} else
				objects.Add(inv.Source);
		}
	}

	public static List<LocatedInventory> DiscoverInventories(
		Rectangle source,
		GameLocation? location,
		IEnumerable<LocatedInventory>? sources,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit = 5,
		int scanLimit = 100,
		int targetLimit = 20,
		bool includeSource = true,
		bool includeDiagonal = true,
		int expandSource = 0
	) {
		List<AbsolutePosition> positions = new();

		for (int x = -expandSource; x < source.Width + expandSource; x++) {
			for (int y = -expandSource; y < source.Height + expandSource; y++) {
				positions.Add(new(
					location,
					new(
						source.X + x,
						source.Y + y
					)
				));
			}
		}

		return DiscoverInventories(
			sources: sources,
			who: who,
			getProvider: getProvider,
			checkConnector: checkConnector,
			distanceLimit: distanceLimit,
			scanLimit: scanLimit,
			targetLimit: targetLimit,
			includeDiagonal: includeDiagonal,
			includeSource: includeSource,
			extra: positions
		);
	}

	public static List<LocatedInventory> DiscoverInventories(
		IEnumerable<LocatedInventory>? sources,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit = 5,
		int scanLimit = 100,
		int targetLimit = 20,
		bool includeSource = true,
		bool includeDiagonal = true,
		IEnumerable<AbsolutePosition>? extra = null
	) {
		List<AbsolutePosition> potentials = new();
		Dictionary<AbsolutePosition, Vector2> origins = new();

		if (extra != null) {
			foreach (var entry in extra) {
				potentials.Add(entry);
				origins[entry] = entry.Position;
			}
		}

		List<LocatedInventory> extra_located = new();

		if (sources != null)
			foreach (LocatedInventory source in sources) {
				var provider = getProvider(source.Source);
				if (provider != null && provider.IsValid(source.Source, source.Location, who)) {
					var rect = provider.GetMultiTileRegion(source.Source, source.Location, who);
					if (rect.HasValue) {
						for (int x = 0; x < rect.Value.Width; x++) {
							for (int y = 0; y < rect.Value.Height; y++) {
								AbsolutePosition abs = new(source.Location, new(
									rect.Value.X + x,
									rect.Value.Y + y
								));

								potentials.Add(abs);
								origins[abs] = abs.Position;
							}
						}

					} else {
						var pos = provider.GetTilePosition(source.Source, source.Location, who);
						if (pos.HasValue) {
							AbsolutePosition abs = new(source.Location, pos.Value);
							potentials.Add(abs);
							origins[abs] = abs.Position;
						} else {
							// We couldn't find it, but we need to assume it's
							// still valid.
							extra_located.Add(new LocatedInventory(source.Source, source.Location));
						}
					}
				}
			}

		int count = potentials.Count;

		for (int i = 0; i < count; i++) {
			var potential = potentials[i];
			AddPotentials(
				potential.Position,
				potential.Position,
				potential.Location,
				potentials,
				origins,
				distanceLimit,
				includeDiagonal
			);
		}

		return WalkPotentials(
			potentials,
			origins,
			includeSource ? 0 : count,
			who,
			getProvider,
			checkConnector,
			distanceLimit,
			scanLimit,
			targetLimit,
			includeDiagonal,
			extra_located
		);
	}

	public static List<LocatedInventory> DiscoverInventories(
		IEnumerable<AbsolutePosition> sources,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit = 5,
		int scanLimit = 100,
		int targetLimit = 20,
		bool includeSource = true,
		bool includeDiagonal = true
	) {
		List<AbsolutePosition> potentials = new(sources);
		Dictionary<AbsolutePosition, Vector2> origins = new();

		foreach (AbsolutePosition source in potentials)
			origins[source] = source.Position;

		int count = potentials.Count;

		for (int i = 0; i < count; i++) {
			var potential = potentials[i];
			AddPotentials(
				potential.Position,
				potential.Position,
				potential.Location,
				potentials,
				origins,
				distanceLimit,
				includeDiagonal
			);
		}

		return WalkPotentials(
			potentials,
			origins,
			includeSource ? 0 : count,
			who,
			getProvider,
			checkConnector,
			distanceLimit,
			scanLimit,
			targetLimit,
			includeDiagonal,
			null
		);
	}

	private static void AddPotentials(
		Vector2 source,
		Vector2 origin,
		GameLocation? location,
		IList<AbsolutePosition> potentials,
		IDictionary<AbsolutePosition, Vector2> origins,
		int distanceLimit,
		bool includeDiagonal
	) {
		for(int x = -1; x < 2; x++) {
			for(int y = -1; y < 2; y++) {
				if (x == 0 && y == 0)
					continue;

				if (!includeDiagonal && x != 0 && y != 0)
					continue;

				int kx = (int) source.X + x;
				int ky = (int) source.Y + y;

				if (Math.Abs(origin.X - kx) > distanceLimit || Math.Abs(origin.Y - ky) > distanceLimit)
					continue;

				AbsolutePosition abs = new(location, new(kx, ky));
				if (!potentials.Contains(abs)) {
					potentials.Add(abs);
					origins[abs] = origin;
				}
			}
		}
	}

	private static List<LocatedInventory> WalkPotentials(
		List<AbsolutePosition> potentials,
		Dictionary<AbsolutePosition, Vector2> origins,
		int start,
		Farmer? who,
		Func<object, IInventoryProvider?> getProvider,
		Func<object, bool>? checkConnector,
		int distanceLimit,
		int scanLimit,
		int targetLimit,
		bool includeDiagonal,
		List<LocatedInventory>? extra
	) {
		List<LocatedInventory> result = new();

		if (extra is not null)
			result.AddRange(extra);

		int i = start;

		while(i < potentials.Count && i < scanLimit) {
			AbsolutePosition abs = potentials[i++];
			SObject? obj;
			SObject? furn;
			TerrainFeature? feature;
			if (abs.Location != null) {
				TileHelper.GetObjectAtPosition(abs.Location, abs.Position, out obj);
				abs.Location.terrainFeatures.TryGetValue(abs.Position, out feature);
				furn = abs.Location.GetFurnitureAt(abs.Position);
			} else {
				feature = null;
				obj = null;
				furn = null;
			}

			bool want_neighbors = false;
			IInventoryProvider? provider;

			if (obj != null) {
				provider = getProvider(obj);
				if (provider != null && provider.IsValid(obj, abs.Location, who)) {
					result.Add(new(obj, abs.Location));
					want_neighbors = true;
				} else if (checkConnector != null && checkConnector(obj))
					want_neighbors = true;
			}

			if (feature != null) {
				provider = getProvider(feature);
				if (provider != null && provider.IsValid(feature, abs.Location, who)) {
					result.Add(new(feature, abs.Location));
					want_neighbors = true;
				} else if (!want_neighbors && checkConnector != null && checkConnector(feature))
					want_neighbors = true;
			}

			if (furn != null) {
				provider = getProvider(furn);
				if (provider != null && provider.IsValid(furn, abs.Location, who)) {
					result.Add(new(furn, abs.Location));
					want_neighbors = true;
				} else if (!want_neighbors && checkConnector != null && checkConnector(furn))
					want_neighbors = true;
			}

			if (result.Count >= targetLimit)
				break;

			if (want_neighbors)
				AddPotentials(abs.Position, origins[abs], abs.Location, potentials, origins, distanceLimit, includeDiagonal);
		}

		return result;
	}

	public static bool DoesLocationContain(GameLocation? location, object? obj) {
		if (location == null || obj == null)
			return false;

		if (location.GetFridge(false) == obj)
			return true;

		if (obj is Furniture furn && location.furniture.Contains(furn))
			return true;

		if (obj is TerrainFeature feature && location.terrainFeatures.Values.Contains(feature))
			return true;

		return location.Objects.Values.Contains(obj);
	}

	public static List<LocatedInventory> LocateInventories(
		IEnumerable<object> inventories,
		IEnumerable<GameLocation> locations,
		Func<object, IInventoryProvider?> getProvider,
		GameLocation? first,
		bool nullLocationValid = false
	) {
		List<LocatedInventory> result = new();

		foreach (object obj in inventories) {
			IInventoryProvider? provider = getProvider(obj);
			if (provider == null)
				continue;

			GameLocation? loc = null;

			if (first != null && DoesLocationContain(first, obj))
				loc = first;
			else {
				foreach(GameLocation location in locations) {
					if (location != first && DoesLocationContain(location, obj)) {
						loc = location;
						break;
					}
				}
			}

			if (loc != null || nullLocationValid)
				result.Add(new(obj, loc));
		}

		return result;
	}

	#endregion

	#region Unsafe Access

	public static List<IBCInventory> GetUnsafeInventories(
		IEnumerable<object> inventories,
		Func<object, IInventoryProvider?> getProvider,
		GameLocation? location,
		Farmer? who,
		bool nullLocationValid = false
	) {
		List<LocatedInventory> located = new();
		foreach (object obj in inventories) {
			if (obj is LocatedInventory inv)
				located.Add(inv);
			else
				located.Add(new(obj, location));
		}

		return GetUnsafeInventories(located, getProvider, who, nullLocationValid);
	}

	public static List<IBCInventory> GetUnsafeInventories(
		IEnumerable<LocatedInventory> inventories,
		Func<object, IInventoryProvider?> getProvider,
		Farmer? who,
		bool nullLocationValid = false
	) {
		List<IBCInventory> result = new();

		foreach (LocatedInventory loc in inventories) {
			if (loc.Location == null && !nullLocationValid)
				continue;

			IInventoryProvider? provider = getProvider(loc.Source);
			if (provider == null || !provider.IsValid(loc.Source, loc.Location, who))
				continue;

			// Try to get the mutex. If we can't, and the mutex is required,
			// then skip this entry.
			NetMutex? mutex = provider.GetMutex(loc.Source, loc.Location, who);
			if (mutex == null && provider.IsMutexRequired(loc.Source, loc.Location, who))
				continue;

			// We don't care about the state of the mutex until we try
			// using it, and this method isn't about using it, so...
			// ignore them for now.

			WorkingInventory entry = new(loc.Source, provider, mutex, loc.Location, who);
			result.Add(entry);
		}

		return result;
	}

	#endregion

	#region Mutex Handling

	public static void WithInventories(
		IEnumerable<LocatedInventory>? inventories,
		Func<object, IInventoryProvider?> getProvider,
		Farmer? who,
		Action<IList<IBCInventory>> withLocks,
		bool nullLocationValid = false
	) {
		WithInventories(inventories, getProvider, who, (locked, onDone) => {
			try {
				withLocks(locked);
			} catch (Exception) {
				onDone();
				throw;
			}

			onDone();
		}, nullLocationValid);
	}

	public static void WithInventories(
		IEnumerable<object>? inventories,
		Func<object, IInventoryProvider?> getProvider,
		GameLocation location,
		Farmer? who,
		Action<IList<IBCInventory>> withLocks,
		bool nullLocationValid = false
	) {
		List<LocatedInventory>? located;
		if (inventories == null)
			located = null;
		else {
			located = new();
			foreach (object obj in inventories) {
				if (obj is LocatedInventory inv)
					located.Add(inv);
				else
					located.Add(new(obj, location));
			}
		}

		WithInventories(located, getProvider, who, (locked, onDone) => {
			try {
				withLocks(locked);
			} catch (Exception) {
				onDone();
				throw;
			}

			onDone();
		}, nullLocationValid);
	}

	public static void WithInventories(
		IEnumerable<LocatedInventory>? inventories,
		Func<object, IInventoryProvider?> getProvider,
		Farmer? who,
		Action<IList<IBCInventory>, Action> withLocks,
		bool nullLocationValid = false,
		IModHelper? helper = null
	) {
		List<IBCInventory> locked = new();
		List<IBCInventory> lockable = new();

		if (inventories != null)
			foreach (LocatedInventory loc in inventories) {
				if (loc.Location == null && !nullLocationValid)
					continue;

				IInventoryProvider? provider = getProvider(loc.Source);
				if (provider == null || !provider.IsValid(loc.Source, loc.Location, who))
					continue;

				// If we can't get a mutex and a mutex is required, then abort.
				NetMutex? mutex = provider.GetMutex(loc.Source, loc.Location, who);
				if (mutex == null && provider.IsMutexRequired(loc.Source, loc.Location, who))
					continue;

				// Check the current state of the mutex. If someone else has
				// it locked, then we can't ensure safety. Abort.
				bool mlocked;

				if (mutex != null) {
					mlocked = mutex.IsLocked();
					if (mlocked && !mutex.IsLockHeld())
						continue;
				} else
					mlocked = true;

				WorkingInventory entry = new(loc.Source, provider, mutex, loc.Location, who);
				if (mlocked)
					locked.Add(entry);
				else
					lockable.Add(entry);
			}

		if (lockable.Count == 0) {
			withLocks(locked, () => { });
			return;
		}

		List<NetMutex> mutexes = lockable.Where(entry => entry.Mutex != null).Select(entry => entry.Mutex!).Distinct().ToList();
		AdvancedMultipleMutexRequest? mmr = null;
		mmr = new AdvancedMultipleMutexRequest(
			mutexes,
			() => {
				locked.AddRange(lockable);
				withLocks(locked, () => {
					mmr?.ReleaseLock();
					mmr = null;
				});
			},
			() => {
				withLocks(locked, () => { });
			},
			helper: helper);
	}

	#endregion

	#region Recipes and Crafting

	/// <summary>
	/// Consume an item from a list of items.
	/// </summary>
	/// <param name="matcher">A method matching the item to consume.</param>
	/// <param name="amount">The quantity to consume.</param>
	/// <param name="items">The list of items to consume the item from.</param>
	/// <param name="nullified">Whether or not any slots in the list are set
	/// to null.</param>
	/// <param name="passed_quality">Whether or not any matching slots were passed
	/// up because they exceeded the <see cref="max_quality"/></param>
	/// <param name="max_quality">The maximum quality of item to consume.</param>
	/// <returns>The number of items remaining to consume.</returns>
	public static int ConsumeItem(Func<Item, bool> matcher, int amount, IList<Item?> items, out bool nullified, out bool passed_quality, int max_quality = int.MaxValue) {

		nullified = false;
		passed_quality = false;

		for (int idx = items.Count - 1; idx >= 0; --idx) {
			Item? item = items[idx];
			if (item == null || ! matcher(item))
				continue;

			// Special logic for Stack Quality
			if (intSQ is not null && intSQ.IsLoaded && item is SObject sobj) {
				amount = intSQ.ConsumeItem(sobj, amount, out bool set_null, out bool set_quality, max_quality);
				if (set_null) {
					items[idx] = null;
					nullified = true;
				}

				if (set_quality)
					passed_quality = true;

				if (amount <= 0)
					return amount;

				continue;
			}

			// Normal logic, without Stack Quality
			int quality = item is SObject obj ? obj.Quality : 0;
			if (quality > max_quality) {
				passed_quality = true;
				continue;
			}

			int count = Math.Min(amount, item.Stack);
			amount -= count;

			if (item.Stack <= count) {
				items[idx] = null;
				nullified = true;

			} else
				item.Stack -= count;

			if (amount <= 0)
				return amount;
		}

		return amount;
	}

	public static int CountItem(Func<Item, bool> matcher, Farmer? who, IEnumerable<Item?>? items, out bool passed_quality, int max_quality = int.MaxValue, int? limit = null) {
		int amount;

		if (who is not null)
			amount = CountItem(matcher, who.Items, out passed_quality, max_quality: max_quality, limit: limit);
		else {
			amount = 0;
			passed_quality = false;
		}

		if (limit is not null && amount >= limit)
			return amount;

		if (items is not null) {
			amount += CountItem(matcher, items, out bool pq, max_quality: max_quality, limit: limit is not null ? limit - amount : null);
			passed_quality |= pq;
		}

		return amount;
	}

	public static int CountItem(Func<Item, bool> matcher, IEnumerable<Item?> items, out bool passed_quality, int max_quality = int.MaxValue, int? limit = null) {
		passed_quality = false;
		int amount = 0;

		foreach(Item? item in items) { 
			if (item == null || !matcher(item))
				continue;

			// Special logic for Stack Quality -- only needed if we're using
			// a maximum quality lower than Iridium.
			if (max_quality < 4 && intSQ is not null && intSQ.IsLoaded && item is SObject sobj) {
				amount += intSQ.CountItem(sobj, out bool set_passed, max_quality);
				if (set_passed)
					passed_quality = true;

				if (limit is not null && amount >= limit)
					return amount;

				continue;
			}

			int quality = item is SObject obj ? obj.Quality : 0;
			if (quality > max_quality) {
				passed_quality = true;
				continue;
			}

			amount += item.Stack;
			if (limit is not null && amount >= limit)
				return amount;
		}

		return amount;
	}

	/// <summary>
	/// Consume matching items from a player, and also from a set of
	/// <see cref="IBCInventory"/> instances.
	/// </summary>
	/// <param name="items">An enumeration of <see cref="KeyValuePair{int,int}"/>
	/// instances where the first integer is the item ID to match and the
	/// second integer is the quantity to consume.
	/// <param name="who">The player to consume items from, if any.</param>
	/// <param name="inventories">An enumeration of <see cref="IBCInventory"/>
	/// instances to consume items from.</param>
	/// <param name="max_quality">The maximum quality of item to consume.</param>
	/// <param name="low_quality_first">Whether or not to consume low quality
	/// items first.</param>
	public static void ConsumeItems(IEnumerable<KeyValuePair<string, int>> items, Farmer? who, IEnumerable<IBCInventory>? inventories, int max_quality = int.MaxValue, bool low_quality_first = false) {
		if (items is null)
			return;

		ConsumeItems(
			items.Select<KeyValuePair<string, int>, (Func<Item, bool>, int)>(x => (item => CraftingRecipe.ItemMatchesForCrafting(item, x.Key), x.Value)),
			who,
			inventories,
			max_quality,
			low_quality_first
		);
	}

	/// <summary>
	/// Consume matching items from a player, and also from a set of
	/// <see cref="IBCInventory"/> instances.
	/// </summary>
	/// <param name="items">An enumeration of tuples where the function
	/// matches items, and the integer is the quantity to consume.</param>
	/// <param name="who">The player to consume items from, if any.</param>
	/// <param name="inventories">An enumeration of <see cref="IBCInventory"/>
	/// instances to consume items from.</param>
	/// <param name="max_quality">The maximum quality of item to consume.</param>
	/// <param name="low_quality_first">Whether or not to consume low quality
	/// items first.</param>
	public static void ConsumeItems(IEnumerable<(Func<Item, bool>, int)> items, Farmer? who, IEnumerable<IBCInventory>? inventories, int max_quality = int.MaxValue, bool low_quality_first = false) {
		IList<IBCInventory>? working = (inventories as IList<IBCInventory>) ?? inventories?.ToList();
		bool[]? modified = working == null ? null : new bool[working.Count];
		IList<Item?>?[] invs = working?.Select(val => val.CanExtractItems() ? val.GetItems() : null).ToArray() ?? Array.Empty<IList<Item?>?>();

		foreach ((Func<Item, bool>, int) pair in items) {
			Func<Item, bool> matcher = pair.Item1;
			int remaining = pair.Item2;

			int mq = max_quality;
			if (low_quality_first)
				mq = 0;

			for (int q = mq; q <= max_quality; q++) {
				bool passed;
				if (who != null)
					remaining = ConsumeItem(matcher, remaining, who.Items, out bool m, out passed, q);
				else
					passed = false;

				if (remaining <= 0)
					break;

				if (working != null)
					for (int iidx = 0; iidx < working.Count; iidx++) {
						IList<Item?>? inv = invs[iidx];
						if (inv == null || inv.Count == 0)
							continue;

						remaining = ConsumeItem(matcher, remaining, inv, out bool modded, out bool p, q);
						if (modded)
							modified![iidx] = true;

						if (p)
							passed = true;

						if (remaining <= 0)
							break;
					}

				if (remaining <= 0 || !passed)
					break;
			}
		}

		if (working != null)
			for (int idx = 0; idx < modified!.Length; idx++) {
				if (modified[idx])
					working[idx].CleanInventory();
			}
	}

	#endregion

	#region Transfer Items

	public static bool AddToInventories(IList<Item?> items, IEnumerable<IBCInventory> inventories, TransferBehavior behavior, Action<Item, int>? onTransfer = null) {
		if (behavior.Mode == TransferMode.None)
			return false;

		int[] transfered = new int[items.Count];

		foreach (IBCInventory inv in inventories) {
			if (!inv.CanInsertItems())
				continue;

			if (FillInventory(items, inv, behavior, transfered, onTransfer))
				return true;
		}

		return false;
	}

	private static bool FillInventory(IList<Item?> items, IBCInventory inventory, TransferBehavior behavior, int[] transfered, Action<Item, int>? onTransfer = null) {
		bool empty = true;

		for(int idx = 0; idx < items.Count; idx++) {
			Item? item = items[idx];
			if (item != null && item.maximumStackSize() > 1 && inventory.IsItemValid(item)) {
				// How many can we transfer?
				int count = item.Stack;
				int transfer;
				switch (behavior.Mode) {
					case TransferMode.All:
						transfer = count;
						break;
					case TransferMode.AllButQuantity:
						transfer = count - behavior.Quantity;
						break;
					case TransferMode.Half:
						transfer = count / 2;
						break;
					case TransferMode.Quantity:
						transfer = behavior.Quantity;
						break;
					case TransferMode.None:
					default:
						return false;
				}

				transfer -= transfered[idx];
				transfer = Math.Clamp(transfer, 0, count);
				if (transfer == 0)
					continue;

				int final;
				bool had_transfered = transfered[idx] > 0;
				if (AddItemToInventory(item, transfer, inventory) is null) {
					items[idx] = null;
					transfered[idx] += count;
					final = -1;
				} else {
					final = item.Stack;
					transfered[idx] += count - item.Stack;

					if (final != (count - transfer))
						empty = false;
				}

				if (count != final && ! had_transfered)
					onTransfer?.Invoke(item, idx);
			}
		}

		return empty;
	}

	public static Item? AddItemToInventory(Item item, int quantity, IBCInventory inventory) {
		int initial = item.Stack;
		if (quantity > initial)
			quantity = initial;

		if (quantity <= 0)
			return item.Stack <= 0 ? null : item;

		bool present = false;

		IList<Item?>? items = inventory.GetItems();
		if (items is null)
			return item;

		foreach(Item? oitem in items) {
			if (oitem is not null && oitem.canStackWith(item)) {
				present = true;
				if (oitem.getRemainingStackSpace() > 0) {
					int remainder = item.Stack - quantity;
					item.Stack = quantity;
					item.Stack = oitem.addToStack(item) + remainder;
					quantity -= (initial - item.Stack);
					if (quantity <= 0 || item.Stack <= remainder)
						return item.Stack <= 0 ? null : item;
				}
			}
		}

		if (!present)
			return item;

		for(int idx = items.Count - 1; idx >= 0; idx--) {
			if (items[idx] == null) {
				if (quantity > item.maximumStackSize()) {
					Item obj = items[idx] = item.getOne();

					int removed = item.maximumStackSize();
					obj.Stack = removed;
					item.Stack -= removed;
					quantity -= removed;

				} else if (quantity < item.Stack) {
					Item obj = items[idx] = item.getOne();
					obj.Stack = quantity;
					item.Stack -= quantity;
					return item;

				} else {
					items[idx] = item;
					return null;
				}
			}
		}

		int capacity = inventory.GetActualCapacity();
		while (capacity > 0 && items.Count < capacity) {
			if (quantity > item.maximumStackSize()) {
				Item obj = item.getOne();
				int removed = item.maximumStackSize();
				obj.Stack = removed;
				item.Stack -= removed;
				quantity -= removed;
				items.Add(obj);

			} else if (quantity < item.Stack) {
				Item obj = item.getOne();
				obj.Stack = quantity;
				item.Stack -= quantity;
				items.Add(obj);
				return item;

			} else {
				items.Add(item);
				return null;
			}
		}

		return item;
	}

	#endregion

}
