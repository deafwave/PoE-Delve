using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace Delve
{
    public class Delve : BaseSettingsPlugin<DelveSettings>
    {
        private HashSet<Entity> _delveEntities = new();
        private string _customImagePath;
        private string _defaultChestImage;
        private string _areaName;
        private bool IsAzuriteMine => _areaName == "Azurite Mine";

        public FossilTiers FossilList = new();

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = { new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal } },
        };

        private const double CameraAngle = 38.7 * Math.PI / 180;

        public override bool Initialise()
        {
            _delveEntities = new HashSet<Entity>();
            _customImagePath = Path.Combine(DirectoryFullName, "Resources");
            _defaultChestImage = Path.Combine(_customImagePath, "DelveChest.png");

            var fossilPath = Path.Combine(DirectoryFullName, "Fossil_Tiers.json");
            if (File.Exists(fossilPath))
            {
                FossilList = JsonConvert.DeserializeObject<FossilTiers>(File.ReadAllText(fossilPath), JsonSettings);
            }
            else
            {
                LogError("Error loading Fossil_Tiers.json, please re-download from the plugin repository.");
            }

            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            _delveEntities.Clear();
            _areaName = area.Name;
        }

        public override void Render()
        {
            if (!Settings.Enable || !IsAzuriteMine) return;

            var mineMap = GameController.IngameState.IngameUi.DelveWindow;

            if (Settings.DelveMineMapConnections.Value && mineMap?.IsVisible == true)
                DrawMineMapConnections(mineMap);

            if (mineMap?.IsVisible != true)
                RenderMapImages();

            if (Settings.DebugHotkey.PressedOnce())
                Settings.DebugMode.Value = !Settings.DebugMode.Value;

            if (Settings.DebugMode.Value)
            {
                foreach (var entity in _delveEntities.ToArray())
                {
                    if (entity.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall") ||
                        entity.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveLight"))
                        continue;
                    if (Settings.ShouldHideOnOpen.Value && entity.GetComponent<Chest>()?.IsOpened == true)
                        continue;

                    var text = entity.Path.Replace("Metadata/Chests/DelveChests/", "");
                    var screenPos = GameController.IngameState.Camera.WorldToScreen(entity.PosNum);
                    var textSize = Graphics.MeasureText(text);

                    Graphics.DrawBox(
                        new RectangleF(screenPos.X - textSize.X / 2 - 10, screenPos.Y - textSize.Y / 2, textSize.X + 20, textSize.Y * 2),
                        Color.White);
                    Graphics.DrawText(text, new Vector2(screenPos.X - textSize.X / 2, screenPos.Y - textSize.Y / 2), Color.Black);
                }
            }
        }

        public override void EntityAdded(Entity entity)
        {
            if (!IsAzuriteMine) return;

            if (entity.Path.StartsWith("Metadata/Chests/DelveChests/")
                || entity.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall")
                || entity.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveLight"))
            {
                _delveEntities.Add(entity);
            }
        }

        public override void EntityRemoved(Entity entity)
        {
            _delveEntities.Remove(entity);
        }

        private void RenderMapImages()
        {
            var map = GameController.IngameState.IngameUi.Map;
            var largeMap = map.LargeMap;

            if (largeMap.IsVisible)
            {
                var camera = GameController.IngameState.Camera;
                var mapRect = largeMap.GetClientRect();
                var playerPos = GameController.Player.GetComponent<Positioned>().GridPosNum;
                var playerPosZ = GameController.Player.GetComponent<Render>().Z;
                var screenCenter = new Vector2(mapRect.Width / 2, mapRect.Height / 2 - 20)
                                   + new Vector2(mapRect.X, mapRect.Y)
                                   + new Vector2(map.LargeMapShiftX, map.LargeMapShiftY);
                var diag = (float)Math.Sqrt(camera.Width * camera.Width + camera.Height * camera.Height);
                var k = camera.Width < 1024f ? 1120f : 1024f;
                var scale = k / camera.Height * camera.Width * 3f / 4f / map.LargeMapZoom;

                foreach (var entity in _delveEntities.ToArray())
                {
                    if (entity is null) continue;
                    var positioned = entity.GetComponent<Positioned>();
                    var render = entity.GetComponent<Render>();
                    if (positioned == null || render == null) continue;
                    var icon = GetMapIcon(entity);
                    if (icon == null) continue;

                    var entityPos = positioned.GridPosNum;
                    var iconZ = render.Z;
                    var point = screenCenter + DeltaInWorldToMinimapDelta(
                        entityPos - playerPos, diag, scale,
                        (iconZ - playerPosZ) / (9f / map.LargeMapZoom));

                    var size = icon.Size * 2;
                    Graphics.DrawImage(icon.ImagePath,
                        new RectangleF(point.X - size / 2f, point.Y - size / 2f, size, size),
                        icon.Color);
                }
            }
            else if (map.SmallMiniMap.IsVisible)
            {
                var smallMinimap = map.SmallMiniMap;
                var playerPos = GameController.Player.GetComponent<Positioned>().GridPosNum;
                var posZ = GameController.Player.GetComponent<Render>().Z;
                const float scale = 240f;
                var mapRect = smallMinimap.GetClientRect();
                var mapCenter = new Vector2(mapRect.X + mapRect.Width / 2, mapRect.Y + mapRect.Height / 2);
                var diag = (float)(Math.Sqrt(mapRect.Width * mapRect.Width + mapRect.Height * mapRect.Height) / 2.0);

                foreach (var entity in _delveEntities.ToArray())
                {
                    if (entity is null) continue;
                    var positioned = entity.GetComponent<Positioned>();
                    var render = entity.GetComponent<Render>();
                    if (positioned == null || render == null) continue;
                    var icon = GetMapIcon(entity);
                    if (icon == null) continue;

                    var entityPos = positioned.GridPosNum;
                    var iconZ = render.Z;
                    var point = mapCenter + DeltaInWorldToMinimapDelta(
                        entityPos - playerPos, diag, scale, (iconZ - posZ) / 20);

                    var size = icon.Size;
                    var rect = new RectangleF(point.X - size / 2f, point.Y - size / 2f, size, size);

                    if (mapRect.Contains(new SharpDX.Vector2(rect.Center.X, rect.Center.Y)))
                    {
                        Graphics.DrawImage(icon.ImagePath, rect, icon.Color);
                    }
                }
            }
        }

        private DelveIcon GetMapIcon(Entity e)
        {
            if (Settings.DelvePathWays)
            {
                if (e.Path == "Metadata/Terrain/Leagues/Delve/Objects/DelveLight")
                {
                    return new DelveIcon(
                        Path.Combine(_customImagePath, "abyss-crack.png"),
                        Settings.DelvePathWaysNodeColor, Settings.DelvePathWaysNodeSize);
                }
            }

            if (Settings.DelveChests)
            {
                if (e.GetComponent<Chest>()?.IsOpened == true) return null;

                if (e.Path.EndsWith("Encounter")) return null;

                // Dynamite supplies
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveMiningSuppliesDynamite")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteGeneric"))
                {
                    if (!Settings.DelveMiningSuppliesDynamiteChest) return null;
                    return new DelveIcon(
                        Path.Combine(_customImagePath, "Bombs.png"),
                        Settings.DelveMiningSuppliesDynamiteChestColor, Settings.DelveMiningSuppliesDynamiteChestSize);
                }

                // Flare supplies
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveMiningSuppliesFlares"))
                {
                    if (!Settings.DelveMiningSuppliesFlaresChest) return null;
                    return new DelveIcon(
                        Path.Combine(_customImagePath, "Flare.png"),
                        Settings.DelveMiningSuppliesFlaresChestColor, Settings.DelveMiningSuppliesFlaresChestSize);
                }

                // Currency chests (off-path, path, generic)
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathCurrency")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/PathCurrency")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericCurrency"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(
                        Path.Combine(_customImagePath, "Currency.png"),
                        Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Random enchant
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericRandomEnchant"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(
                        Path.Combine(_customImagePath, "Enchant.png"),
                        Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // 6-links
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmour6LinkedUniqueBody")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeapon6LinkedTwoHanded")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourFullyLinkedBody"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(
                        Path.Combine(_customImagePath, "SixLink.png"),
                        Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Dynamite currency / DelveChestCurrency variants
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteCurrency")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrency"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    string tempPath = e.Path.Replace("Metadata/Chests/DelveChests/", "");

                    if (tempPath.Contains("Currency1") || tempPath == "DynamiteCurrency" || tempPath.Contains("DelveChestCurrencyHighShards"))
                        return new DelveIcon(Path.Combine(_customImagePath, "Currency.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                    if (tempPath.Contains("Currency2"))
                        return new DelveIcon(Path.Combine(_customImagePath, "Currency.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize.Value * 2f);
                    if (tempPath.Contains("Currency3"))
                        return new DelveIcon(Path.Combine(_customImagePath, "Currency.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize.Value * 3f);
                    if (tempPath.Contains("DelveChestCurrencySilverCoins"))
                        return new DelveIcon(Path.Combine(_customImagePath, "SilverCoin.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                    if (tempPath.Contains("DelveChestCurrencyWisdomScrolls"))
                        return new DelveIcon(Path.Combine(_customImagePath, "WisDomCurrency.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                    if (tempPath.Contains("DelveChestCurrencyDivination"))
                        return new DelveIcon(Path.Combine(_customImagePath, "divinationCard.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                    if (tempPath.Contains("DelveChestCurrencyMaps"))
                        return new DelveIcon(Path.Combine(_customImagePath, "Map.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                    if (tempPath.Contains("DelveChestCurrencyVaal"))
                        return new DelveIcon(Path.Combine(_customImagePath, "Corrupted.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                    if (tempPath.Contains("DelveChestCurrencySockets"))
                        return new DelveIcon(Path.Combine(_customImagePath, "AdditionalSockets.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Additional sockets
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourBody2AdditionalSockets"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "AdditionalSockets.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Atziri fragments
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericAtziriFragment"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "Fragment.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Pale Court fragments
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericPaleCourtFragment"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "PaleCourtComplete.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Essences
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericRandomEssence")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialGenericEssence"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "Essence.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Divination cards
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericDivination")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourDivination")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponDivination")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsDivination"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "divinationCard.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Azurite veins
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveAzuriteVein"))
                {
                    if (!Settings.DelveAzuriteVeinChest) return null;
                    int size = Settings.DelveAzuriteVeinChestSize;
                    string azuriteImage = "";
                    if (e.Path.EndsWith("1_1")) { azuriteImage = "AzuriteT3.png"; }
                    else if (e.Path.EndsWith("1_2")) { azuriteImage = "AzuriteT3.png"; }
                    else if (e.Path.EndsWith("1_3")) { azuriteImage = "AzuriteT2.png"; size *= 2; }
                    else if (e.Path.EndsWith("2_1")) { azuriteImage = "AzuriteT1.png"; size *= 2; }
                    if (azuriteImage != "")
                        return new DelveIcon(Path.Combine(_customImagePath, azuriteImage), Color.White, size);
                }

                // Resonators T1 (3-5 socket)
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator3")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator4")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator5"))
                {
                    if (!Settings.DelveResonatorChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "ResonatorT1.png"), Settings.DelveResonatorChestColor, Settings.DelveResonatorChestSize * 0.7f);
                }

                // Resonator T2 (2 socket)
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator2"))
                {
                    if (!Settings.DelveResonatorChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "ResonatorT2.png"), Settings.DelveResonatorChestColor, Settings.DelveResonatorChestSize * 0.7f);
                }

                // Resonator T3 (1 socket)
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator1"))
                {
                    if (!Settings.DelveResonatorChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "ResonatorT3.png"), Settings.DelveResonatorChestColor, Settings.DelveResonatorChestSize * 0.7f);
                }

                // Movement speed armour
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourMovementSpeed"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "SpeedArmour.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Unique mana flask
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueMana"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "UniqueManaFlask.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize * 1.3f);
                }

                // Fossils (tiered)
                if (e.Path.StartsWith("Metadata/Chests/DelveChests")
                    && (e.Path.EndsWith("FossilChest") || e.Path.EndsWith("FossilChestDynamite")))
                {
                    if (!Settings.DelveFossilChest) return null;
                    foreach (var s in FossilList.T1)
                        if (e.Path.ToLower().Contains(s.ToLower()))
                            return new DelveIcon(Path.Combine(_customImagePath, "AbberantFossilT1.png"), Settings.DelveFossilChestColor, Settings.DelveFossilChestSize);
                    foreach (var s in FossilList.T2)
                        if (e.Path.ToLower().Contains(s.ToLower()))
                            return new DelveIcon(Path.Combine(_customImagePath, "AbberantFossilT2.png"), Settings.DelveFossilChestColor, Settings.DelveFossilChestSize);
                    foreach (var s in FossilList.T3)
                        if (e.Path.ToLower().Contains(s.ToLower()))
                            return new DelveIcon(Path.Combine(_customImagePath, "AbberantFossilT3.png"), Settings.DelveFossilChestColor, Settings.DelveFossilChestSize);
                    return new DelveIcon(Path.Combine(_customImagePath, "AbberantFossil.png"), Settings.DelveFossilChestColor, Settings.DelveFossilChestSize);
                }

                // City Vaal resonator / generic resonator
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalResonator")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator"))
                {
                    if (!Settings.DelveResonatorChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "ResonatorT1.png"), Settings.DelveResonatorChestColor, Settings.DelveResonatorChestSize * 0.7f);
                }

                // Maps
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestMap"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "Map.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Corrupted items
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourCorrupted")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCorrupted")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsCorrupted")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalDoubleCorrupted"))
                {
                    if (!Settings.DelveCurrencyChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "Corrupted.png"), Settings.DelveCurrencyChestColor, Settings.DelveCurrencyChestSize);
                }

                // Breakable walls
                if (e.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall"))
                {
                    if (!Settings.DelveWall) return null;
                    return e.IsAlive
                        ? new DelveIcon(Path.Combine(_customImagePath, "gate.png"), Settings.DelveWallColor, Settings.DelveWallSize)
                        : new DelveIcon(Path.Combine(_customImagePath, "hidden_door.png"), Settings.DelveWallColor, Settings.DelveWallSize);
                }

                // --- Generic / league-specific chests (use default chest icon) ---

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathGeneric")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathGeneric"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathArmour")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathArmour")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteArmour"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathWeapon")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathWeapon")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteWeapon"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathTrinkets")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathTrinkets")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericTrinkets")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteTrinkets"))
                    return new DelveIcon(Path.Combine(_customImagePath, "rare-amulet.png"), Color.White, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/ProsperoChest"))
                    return new DelveIcon(Path.Combine(_customImagePath, "PerandusCoins.png"), Color.White, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericAdditionalUniques")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourMultipleUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponMultipleUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsMultipleUnique"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericAdditionalUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniquePhysical")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueFire")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueCold")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueLightning")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueChaos")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueMana"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericProphecyItem"))
                    return new DelveIcon(Path.Combine(_customImagePath, "Prophecy.png"), Color.White, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericElderItem")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsElder")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourElder")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponElder"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericShaperItem")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponShaper")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsShaper")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourShaper"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericOffering"))
                    return new DelveIcon(Path.Combine(_customImagePath, "OfferingChest.png"), Color.White, Settings.DelvePathwayChestSize);

                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericDelveUnique"))
                    return new DelveIcon(Path.Combine(_customImagePath, "DelveChest.png"), Color.White, Settings.DelvePathwayChestSize);

                // League-specific chests with custom icons
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueOnslaught"))
                    return new DelveIcon(Path.Combine(_customImagePath, "OnslaughtChest.png"), Color.White, Settings.DelvePathwayChestSize * 2f);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueAnarchy"))
                    return new DelveIcon(Path.Combine(_customImagePath, "OnslaughtChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueAmbushInvasion"))
                    return new DelveIcon(Path.Combine(_customImagePath, "AmbushInvasionChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueDomination"))
                    return new DelveIcon(Path.Combine(_customImagePath, "DominationChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueNemesis"))
                    return new DelveIcon(Path.Combine(_customImagePath, "NemesisChest.png"), Color.White, Settings.DelvePathwayChestSize * 2f);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueRampage"))
                    return new DelveIcon(Path.Combine(_customImagePath, "RampageChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBeyond"))
                    return new DelveIcon(Path.Combine(_customImagePath, "BeyondChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssUnique"))
                    return new DelveIcon(Path.Combine(_customImagePath, "AbyssChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBloodlines"))
                    return new DelveIcon(Path.Combine(_customImagePath, "BloodlinesChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueTorment"))
                    return new DelveIcon(Path.Combine(_customImagePath, "TormentChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueWarbands"))
                    return new DelveIcon(Path.Combine(_customImagePath, "WarbandsChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueTempest"))
                    return new DelveIcon(Path.Combine(_customImagePath, "TempestChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueTalisman"))
                    return new DelveIcon(Path.Combine(_customImagePath, "TalismanChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeaguePerandus"))
                    return new DelveIcon(Path.Combine(_customImagePath, "PerandusChest.png"), Color.White, Settings.DelvePathwayChestSize * 2f);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBreach"))
                    return new DelveIcon(Path.Combine(_customImagePath, "BreachChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueHarbinger"))
                    return new DelveIcon(Path.Combine(_customImagePath, "HarbingerChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueAbyss"))
                    return new DelveIcon(Path.Combine(_customImagePath, "AbyssChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBestiary"))
                    return new DelveIcon(Path.Combine(_customImagePath, "BestiaryChest.png"), Color.White, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueIncursion"))
                    return new DelveIcon(Path.Combine(_customImagePath, "IncursionChest.png"), Color.White, Settings.DelvePathwayChestSize);

                // Generic chest types with default icon
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourMultipleResists")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourLife")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourAtlas")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourOfCrafting")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsOfCrafting")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponPhysicalDamage")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCaster")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponExtraQuality")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCannotRollCaster")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCannotRollAttacker")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeapon30QualityUnique")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsAmulet")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsRing")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsJewel")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsAtlas")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsEyesOfTheGreatwolf")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemGCP")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemHighQuality")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemHighLevel")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemHighLevelQuality")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemLevel4Special")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestMapChisels")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestMapCorrupted")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestAssortedFossils")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialArmourMinion")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialTrinketsMinion")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialGenericMinion")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalImplicit")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalAtzoatlRare")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalUberAtziri")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalDoubleCorrupted")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalEmblem"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                // Stygian Vise
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssStygian"))
                    return new DelveIcon(Path.Combine(_customImagePath, "StygianViseChest.png"), Color.White, Settings.DelvePathwayChestSize);

                // Abyss jewels
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssJewels")
                    || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssHighJewel"))
                    return new DelveIcon(Path.Combine(_customImagePath, "AbyssJewelChest.png"), Color.White, Settings.DelvePathwayChestSize);

                // City Vaal Azurite
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalAzurite"))
                    return new DelveIcon(Path.Combine(_customImagePath, "AzuriteT1.png"), Color.White, Settings.DelvePathwayChestSize);

                // City Vaal Fossils
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalFossils"))
                {
                    if (!Settings.DelveFossilChest) return null;
                    return new DelveIcon(Path.Combine(_customImagePath, "AbberantFossil.png"), Settings.DelveFossilChestColor, Settings.DelveFossilChestSize);
                }

                // Broad category catch-alls
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecial"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGem"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinkets"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeapon"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmour"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);
                if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGeneric"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);

                // Catch-all for any remaining delve chests
                if (Settings.DelvePathwayChest && e.Path.StartsWith("Metadata/Chests/DelveChests") && !e.Path.Contains("Encounter"))
                    return new DelveIcon(_defaultChestImage, Settings.DelvePathwayChestColor, Settings.DelvePathwayChestSize);
            }

            return null;
        }

        private void DrawMineMapConnections(dynamic mineMap)
        {
            var gridElement = mineMap.GridElement;
            if (mineMap == null || !mineMap.IsVisible) return;

            var gridElementArea = gridElement.GetClientRect();
            float reducedWidth = (100 - Settings.ShowRadiusPercentage.Value) * gridElementArea.Width / 200;
            float reduceHeight = (100 - Settings.ShowRadiusPercentage.Value) * gridElementArea.Height / 200;
            gridElementArea.Inflate(-reducedWidth, -reduceHeight);

            if (gridElement == null) return;

            foreach (var delveBigCell in gridElement.Cells)
            {
                foreach (var cell in delveBigCell.Cells)
                {
                    if (gridElementArea.Contains(new SharpDX.Vector2(cell.GetClientRect().Center.X, cell.GetClientRect().Center.Y)))
                    {
                        Graphics.DrawFrame(cell.GetClientRect(), Color.Yellow, 1); // Draws but is not useful
                        // if(cell.Info.Interesting) // Is never True
                        // connection doesn't exist
                        // 'mines' data is not useful
                        // Art is always `Art/Textures/Interface/2D/2DArt/UIImages/InGame/15.dds` or `⠠﹛ҡ`
                        // foreach (var connection in cell.Children)
                        // {
                        //     var width = (int)connection.Width;
                        //     if (width == 10 || width == 4)
                        //         Graphics.DrawFrame(connection.GetClientRect(), Color.Yellow, 1);
                        // }
                    }
                }
            }
        }

        private static Vector2 DeltaInWorldToMinimapDelta(Vector2 delta, double diag, float scale, float deltaZ = 0)
        {
            var cos = (float)(diag * Math.Cos(CameraAngle));
            var sin = (float)(diag * Math.Sin(CameraAngle));
            return new Vector2((delta.X - delta.Y) * cos / scale, (deltaZ - (delta.X + delta.Y) * sin) / scale);
        }

        private class DelveIcon
        {
            public string ImagePath;
            public Color Color;
            public float Size;

            public DelveIcon(string imagePath, Color color, float size)
            {
                ImagePath = imagePath;
                Color = color;
                Size = size;
            }
        }

        public class FossilTiers
        {
            [JsonProperty("t1")] public string[] T1 { get; set; }
            [JsonProperty("t2")] public string[] T2 { get; set; }
            [JsonProperty("t3")] public string[] T3 { get; set; }
        }
    }
}
