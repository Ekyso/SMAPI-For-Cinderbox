using System.Collections.Generic;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.Extensions;

/// <summary>Provides internal extensions for <see cref="IClickableMenu"/>.</summary>
internal static class ClickableMenuExtensions
{
    /// <param name="menu">The clickable menu to extend.</param>
    extension(IClickableMenu menu)
    {
        /// <summary>Get a string representation of the menu chain to the given menu (including the specified menu), in parent to child order.</summary>
        public string GetMenuChainLabel()
        {
            Stack<string> chain = [];

            for (; menu != null; menu = menu.GetParentMenu())
                chain.Push(menu.GetType().FullName!);

            return string.Join(" > ", chain);
        }
    }
}
