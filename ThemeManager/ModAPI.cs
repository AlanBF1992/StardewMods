using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

using Leclair.Stardew.Common;
using Leclair.Stardew.ThemeManager.Models;
using Leclair.Stardew.ThemeManager.Serialization;
using Leclair.Stardew.ThemeManager.VariableSets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Newtonsoft.Json;

using StardewModdingAPI;

using StardewValley.BellsAndWhistles;

using static Leclair.Stardew.ThemeManager.IThemeManagerApi;

namespace Leclair.Stardew.ThemeManager;

public class ModAPI : IThemeManagerApi {

	internal readonly ModEntry Mod;
	internal readonly IManifest Other;
	internal readonly RealVariableSetConverter Converter;

	internal ModAPI(ModEntry mod, IManifest other) {
		Mod = mod;
		Other = other;

		Converter = new RealVariableSetConverter(other.UniqueID);

		Mod.GameThemeManager!.ThemeChanged += BaseThemeManager_ThemeChanged;
	}

	private void BaseThemeManager_ThemeChanged(IThemeChangedEvent<GameTheme> e) {
		GameThemeChanged?.Invoke(new ThemeChangedEventArgs<IGameTheme>(
			e.OldId, e.OldManifest, e.OldData,
			e.NewId, e.NewManifest, e.NewData
		)); //, monitor: Mod.Monitor);
	}

	#region Base Theme

	public IGameTheme GameTheme => Mod.GameTheme!;

	public event Action<IThemeChangedEvent<IGameTheme>>? GameThemeChanged;

	#endregion

	#region Variable Sets

	/// <inheritdoc />
	public JsonConverter VariableSetConverter => Converter;

	public bool RegisterVariableSetType<TValue>(TryParseVariableSetValue<TValue> parseDelegate) {
		return TypedVariableSet<object>.RegisterType(parseDelegate);
	}

	public IVariableSet<IManagedAsset<Texture2D>> CreateTextureVariableSet() {
		return new TextureVariableSet();
	}

	public IVariableSet<IManagedAsset<SpriteFont>> CreateFontVariableSet() {
		return new FontVariableSet();
	}

	public IVariableSet<IManagedAsset<IBmFontData>> CreateBmFontVariableSet() {
		return new BmFontVariableSet();
	}

	public IVariableSet<TValue> CreateVariableSet<TValue>() {
		Type tType = typeof(TValue);
		if (tType == typeof(Color))
			return (IVariableSet<TValue>) new ColorVariableSet();
		if (tType == typeof(float))
			return (IVariableSet<TValue>) new FloatVariableSet();
		if (tType == typeof(IManagedAsset<IBmFontData>))
			return (IVariableSet<TValue>) new BmFontVariableSet();
		if (tType == typeof(IManagedAsset<SpriteFont>))
			return (IVariableSet<TValue>) new FontVariableSet();
		if (tType == typeof(IManagedAsset<Texture2D>))
			return (IVariableSet<TValue>) new TextureVariableSet();

		if (TypedVariableSet<object>.CanHandleType(tType) && TypedVariableSet<object>.CreateInstance(tType) is IVariableSet<TValue> tvs)
			return tvs;

		throw new ArgumentException(nameof(TValue));
	}

	#endregion

	#region Custom Themes

	[Obsolete("Use the method that takes onThemeChanged")]
	public IThemeManager<DataT> GetOrCreateManager<DataT>(DataT? defaultTheme = null, string? embeddedThemesPath = "assets/themes", string? assetPrefix = "assets", string? assetLoaderPrefix = null, string? themeLoaderPath = null, bool? forceAssetRedirection = null, bool? forceThemeRedirection = null) where DataT : class, new() {
		return GetOrCreateManager<DataT>(
			defaultTheme: defaultTheme,
			embeddedThemesPath: embeddedThemesPath,
			assetPrefix: assetPrefix,
			assetLoaderPrefix: assetLoaderPrefix,
			themeLoaderPath: themeLoaderPath,
			forceAssetRedirection: forceAssetRedirection,
			forceThemeRedirection: forceThemeRedirection,
			onThemeChanged: null
		);
	}

	public IThemeManager<DataT> GetOrCreateManager<DataT>(DataT? defaultTheme = null, string? embeddedThemesPath = "assets/themes", string? assetPrefix = "assets", string? assetLoaderPrefix = null, string? themeLoaderPath = null, bool? forceAssetRedirection = null, bool? forceThemeRedirection = null, Action<IThemeChangedEvent<DataT>>? onThemeChanged = null) where DataT : class, new() {
		IThemeManager<DataT>? manager;
		lock ((Mod.Managers as ICollection).SyncRoot) {
			if (Mod.Managers.TryGetValue(Other, out var mdata)) {
				manager = mdata.Item2 as IThemeManager<DataT>;
				if (manager is null || mdata.Item1 != typeof(DataT))
					throw new InvalidCastException($"Cannot convert {mdata.Item1} to {typeof(DataT)}");

				return manager;
			}
		}

		if (!Mod.Config.SelectedThemes.TryGetValue(Other.UniqueID, out string? selected))
			selected = "automatic";

		manager = new ThemeManager<DataT>(
			mod: Mod,
			other: Other,
			selectedThemeId: selected,
			defaultTheme: defaultTheme,
			embeddedThemesPath: embeddedThemesPath,
			assetPrefix: assetPrefix,
			assetLoaderPrefix: assetLoaderPrefix,
			themeLoaderPath: themeLoaderPath,
			forceAssetRedirection: forceAssetRedirection,
			forceThemeRedirection: forceThemeRedirection
		);

		if (onThemeChanged is not null)
			manager.ThemeChanged += onThemeChanged;

		lock ((Mod.Managers as ICollection).SyncRoot) {
			Mod.Managers[Other] = (typeof(DataT), manager);
		}

		if (!string.IsNullOrEmpty(manager.AssetLoaderPrefix))
			lock ((Mod.ManagersByAssetPrefix as ICollection).SyncRoot) {
				Mod.ManagersByAssetPrefix[manager.AssetLoaderPrefix] = (IThemeManagerInternal) manager;
			}

		if (manager.UsingThemeRedirection && !string.IsNullOrEmpty(manager.ThemeLoaderPath))
			lock ((Mod.ManagersByThemeAsset as ICollection).SyncRoot) {
				Mod.ManagersByThemeAsset[manager.ThemeLoaderPath] = (IThemeManagerInternal) manager;
			}

		manager.Discover();
		return manager;
	}

	public IThemeManager? GetManager(IManifest? forMod = null) {
		lock ((Mod.Managers as ICollection).SyncRoot) {
			if (!Mod.Managers.TryGetValue(forMod ?? Other, out var manager)) {
				return null;
			}

			return manager.Item2;
		}
	}

	public bool TryGetManager([NotNullWhen(true)] out IThemeManager? themeManager, IManifest? forMod = null) {
		lock ((Mod.Managers as ICollection).SyncRoot) {
			if (!Mod.Managers.TryGetValue(forMod ?? Other, out var manager)) {
				themeManager = null;
				return false;
			}

			themeManager = manager.Item2;
			return true;
		}
	}

	public IThemeManager<DataT>? GetTypedManager<DataT>(IManifest? forMod = null) where DataT : class, new() {
		lock ((Mod.Managers as ICollection).SyncRoot) {
			if (!Mod.Managers.TryGetValue(forMod ?? Other, out var manager) || manager.Item1 != typeof(DataT))
				return null;

			return manager.Item2 as IThemeManager<DataT>;
		}
	}

	public bool TryGetTypedManager<DataT>([NotNullWhen(true)] out IThemeManager<DataT>? themeManager, IManifest? forMod = null) where DataT : class, new() {
		lock ((Mod.Managers as ICollection).SyncRoot) {
			if (!Mod.Managers.TryGetValue(forMod ?? Other, out var manager) || manager.Item1 != typeof(DataT)) {
				themeManager = null;
				return false;
			}

			themeManager = manager.Item2 as IThemeManager<DataT>;
			return themeManager is not null;
		}
	}

	#endregion

	#region Color Parsing

	public bool TryParseColor(string value, [NotNullWhen(true)] out Color? color) {
		if (string.IsNullOrWhiteSpace(value)) {
			color = default;
			return false;
		}

		if (value.StartsWith('$')) {
			if (GameTheme is not null && GameTheme.ColorVariables.TryGetValue(value[1..], out var c)) {
				color = c;
				return true;
			}

			color = default;
			return false;
		}

		return CommonHelper.TryParseColor(value, out color);
	}

	#endregion

	#region Font-ed SpriteText

	public void DrawSpriteText(SpriteBatch batch, IBmFontData? font, string text, int x, int y, Color? color, float alpha = 1f) {
		if (font is not null)
			Mod.SpriteTextManager.ApplyFont(null, null, font);

		SpriteText.drawString(batch, text, x, y, alpha: alpha, color: color);

		if (font is not null)
			Mod.SpriteTextManager.ApplyNormalFont();
	}

	#endregion

}
