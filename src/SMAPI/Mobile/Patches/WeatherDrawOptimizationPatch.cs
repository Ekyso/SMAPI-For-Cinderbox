#if SMAPI_FOR_ANDROID
using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Mods;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Optimizes Game1.drawWeather by hoisting loop-invariant calculations for snow
/// and rain rendering outside their per-particle loops.
/// </summary>
internal static class WeatherDrawOptimizationPatch
{
    /// <summary>Cached FieldInfo for Game1.hooks (protected internal field).</summary>
    private static FieldInfo? _hooksField;

    /// <summary>Reusable buffer for rain source rectangles (4 frames).</summary>
    private static readonly Rectangle[] _rainRects = new Rectangle[4];

    /// <summary>Get the current Game1.hooks value via reflection.</summary>
    private static ModHooks GetHooks()
    {
        _hooksField ??= typeof(Game1).GetField("hooks",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return (ModHooks)_hooksField!.GetValue(null)!;
    }

    /// <summary>Apply the weather draw optimization patch.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            var target = AccessTools.Method(
                typeof(Game1),
                "drawWeather",
                new[] { typeof(GameTime), typeof(RenderTarget2D) }
            );

            if (target == null)
            {
                AndroidLogger.Log(
                    "[WeatherDrawOptimizationPatch] Could not find Game1.drawWeather"
                );
                return;
            }

            var prefix = new HarmonyMethod(
                typeof(WeatherDrawOptimizationPatch).GetMethod(
                    nameof(DrawWeather_Prefix),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
            );

            harmony.Patch(target, prefix: prefix);
            AndroidLogger.Log("[WeatherDrawOptimizationPatch] Applied weather draw optimization");
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[WeatherDrawOptimizationPatch] Failed to apply: {ex}");
        }
    }

    /// <summary>
    /// Optimized replacement for Game1.drawWeather.
    /// Identical output to the original, with loop-invariant calculations hoisted.
    /// </summary>
    private static bool DrawWeather_Prefix(Game1 __instance, GameTime time, RenderTarget2D target_screen)
    {
        try
        {
            Game1.spriteBatch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.PointClamp);

            if (GetHooks().OnRendering(RenderSteps.World_Weather, Game1.spriteBatch, time, target_screen)
                && Game1.currentLocation.IsOutdoors)
            {
                if (Game1.currentLocation.IsSnowingHere())
                {
                    Game1.snowPos.X %= 64f;
                    Vector2 v = default;

                    int snowFrame = (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1200.0) / 75 * 16;
                    Rectangle snowSourceRect = new Rectangle(368 + snowFrame, 192, 16, 16);
                    Color snowColor = Color.White * 0.8f * Game1.options.snowTransparency;

                    for (float x = -64f + Game1.snowPos.X % 64f; x < (float)Game1.viewport.Width; x += 64f)
                    {
                        for (float y = -64f + Game1.snowPos.Y % 64f; y < (float)Game1.viewport.Height; y += 64f)
                        {
                            v.X = (int)x;
                            v.Y = (int)y;
                            Game1.spriteBatch.Draw(
                                Game1.mouseCursors, v, snowSourceRect, snowColor,
                                0f, Vector2.Zero, 4.001f, SpriteEffects.None, 1f
                            );
                        }
                    }
                }

                if (!Game1.currentLocation.ignoreDebrisWeather.Value && Game1.currentLocation.IsDebrisWeatherHere())
                {
                    if (__instance.takingMapScreenshot)
                    {
                        if (Game1.debrisWeather != null)
                        {
                            foreach (WeatherDebris w in Game1.debrisWeather)
                            {
                                Vector2 position = w.position;
                                w.position = new Vector2(
                                    Game1.random.Next(Game1.viewport.Width - w.sourceRect.Width * 3),
                                    Game1.random.Next(Game1.viewport.Height - w.sourceRect.Height * 3)
                                );
                                w.draw(Game1.spriteBatch);
                                w.position = position;
                            }
                        }
                    }
                    else if (Game1.viewport.X > -Game1.viewport.Width)
                    {
                        foreach (WeatherDebris item in Game1.debrisWeather)
                        {
                            item.draw(Game1.spriteBatch);
                        }
                    }
                }

                if (Game1.currentLocation.IsRainingHere()
                    && !(Game1.currentLocation is Summit)
                    && (!Game1.eventUp || Game1.currentLocation.isTileOnMap(new Vector2(Game1.viewport.X / 64, Game1.viewport.Y / 64))))
                {
                    bool isGreenRain = Game1.IsGreenRainingHere();
                    Color rainColor = isGreenRain ? Color.LimeGreen : Color.White;
                    int vibrancy = isGreenRain ? 2 : 1;
                    int greenOffset = isGreenRain ? 4 : 0;

                    // pre-calculate all 4 source rectangles outside the particle loop
                    for (int f = 0; f < 4; f++)
                    {
                        _rainRects[f] = Game1.getSourceRectForStandardTileSheet(
                            Game1.rainTexture, f + greenOffset, 16, 16
                        );
                    }

                    for (int i = 0; i < Game1.rainDrops.Length; i++)
                    {
                        Rectangle srcRect = _rainRects[Game1.rainDrops[i].frame];
                        for (int j = 0; j < vibrancy; j++)
                        {
                            Game1.spriteBatch.Draw(
                                Game1.rainTexture, Game1.rainDrops[i].position, srcRect, rainColor,
                                0f, Vector2.Zero, 4f, SpriteEffects.None, 1f
                            );
                        }
                    }
                }
            }

            GetHooks().OnRendered(RenderSteps.World_Weather, Game1.spriteBatch, time, target_screen);
            Game1.spriteBatch.End();
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[WeatherDrawOptimizationPatch] Error in DrawWeather_Prefix: {ex.Message}");

            try { Game1.spriteBatch.End(); } catch { }
            return true;
        }

        return false;
    }
}
#endif
