using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.Reflection;

namespace StardewModdingAPI.Framework.Extensions;

/// <summary>Provides internal extensions for <see cref="SpriteBatch"/>.</summary>
internal static class SpriteBatchExtensions
{
    /// <param name="spriteBatch">The sprite batch to extend.</param>
    extension(SpriteBatch spriteBatch)
    {
        /// <summary>Get whether the sprite batch is between a begin and end pair.</summary>
        /// <param name="reflection">The reflection helper with which to access private fields.</param>
        public bool IsOpen(Reflector reflection)
        {
            return reflection.GetField<bool>(spriteBatch, "_beginCalled").GetValue();
        }
    }
}
