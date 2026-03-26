using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.Events;
using StardewModdingAPI.Framework.Reflection;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework;

/// <summary>Provides extension methods for SMAPI's internal use.</summary>
internal static class InternalExtensions
{
    /*********
    ** Public methods
    *********/
    /****
    ** IMonitor
    ****/
    /// <param name="monitor">The monitor to extend.</param>
    extension(IMonitor monitor)
    {
        /// <summary>Log a message for the player or developer the first time it occurs.</summary>
        /// <param name="hash">The hash of logged messages.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void LogOnce(HashSet<string> hash, string message, LogLevel level = LogLevel.Trace)
        {
            if (!hash.Contains(message))
            {
                monitor.Log(message, level);
                hash.Add(message);
            }
        }
    }

    /****
    ** IModMetadata
    ****/
    /// <param name="metadata">The mod metadata to extend.</param>
    extension(IModMetadata metadata)
    {
        /// <summary>Log a message using the mod's monitor.</summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void LogAsMod(string message, LogLevel level = LogLevel.Trace)
        {
            if (metadata.Monitor is null)
                throw new InvalidOperationException($"Can't log as mod {metadata.DisplayName}: mod is broken or a content pack. Logged message:\n[{level}] {message}");

            metadata.Monitor.Log(message, level);
        }

        /// <summary>Log a message using the mod's monitor, but only if it hasn't already been logged since the last game launch.</summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void LogAsModOnce(string message, LogLevel level = LogLevel.Trace)
        {
            metadata.Monitor?.LogOnce(message, level);
        }
    }

    /****
    ** ManagedEvent
    ****/
    /// <typeparam name="TEventArgs">The event args type to construct.</typeparam>
    /// <param name="event">The event to extend.</param>
    extension<TEventArgs>(ManagedEvent<TEventArgs> @event)
        where TEventArgs : new()
    {
        /// <summary>Raise the event using the default event args and notify all handlers.</summary>
        public void RaiseEmpty()
        {
            if (@event.HasListeners)
                @event.Raise(Singleton<TEventArgs>.Instance);
        }
    }

    /****
    ** ReaderWriterLockSlim
    ****/
    /// <param name="lock">The lock to extend.</param>
    extension(ReaderWriterLockSlim @lock)
    {
        /// <summary>Run code within a read lock.</summary>
        /// <param name="action">The action to perform.</param>
        public void InReadLock(Action action)
        {
            @lock.EnterReadLock();
            try
            {
                action();
            }
            finally
            {
                @lock.ExitReadLock();
            }
        }

        /// <summary>Run code within a read lock.</summary>
        /// <typeparam name="TReturn">The action's return value.</typeparam>
        /// <param name="action">The action to perform.</param>
        public TReturn InReadLock<TReturn>(Func<TReturn> action)
        {
            @lock.EnterReadLock();
            try
            {
                return action();
            }
            finally
            {
                @lock.ExitReadLock();
            }
        }

        /// <summary>Run code within a write lock.</summary>
        /// <param name="action">The action to perform.</param>
        public void InWriteLock(Action action)
        {
            @lock.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }

        /// <summary>Run code within a write lock.</summary>
        /// <typeparam name="TReturn">The action's return value.</typeparam>
        /// <param name="action">The action to perform.</param>
        public TReturn InWriteLock<TReturn>(Func<TReturn> action)
        {
            @lock.EnterWriteLock();
            try
            {
                return action();
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
    }

    /****
    ** IActiveClickableMenu
    ****/
    /// <param name="menu">The clickable menu to extend.</param>
    extension(IClickableMenu menu)
    {
        /// <summary>Get a string representation of the menu chain to the given menu (including the specified menu), in parent to child order.</summary>
        public string GetMenuChainLabel()
        {
            static IEnumerable<IClickableMenu> GetAncestors(IClickableMenu menu)
            {
                for (; menu != null; menu = menu.GetParentMenu())
                    yield return menu;
            }

            return string.Join(" > ", GetAncestors(menu).Reverse().Select(p => p.GetType().FullName));
        }
    }

    /****
    ** Sprite batch
    ****/
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

    /****
    ** Texture2D
    ****/
    /// <param name="texture">The texture to extend.</param>
    extension(Texture2D? texture)
    {
        /// <summary>Set the texture name field.</summary>
        /// <param name="assetName">The asset name to set.</param>
        /// <returns>Returns the texture for chaining.</returns>
        [return: NotNullIfNotNull(nameof(texture))]
        public Texture2D? SetName(IAssetName assetName)
        {
            texture?.Name = assetName.Name;

            return texture;
        }

        /// <summary>Set the texture name field.</summary>
        /// <param name="assetName">The asset name to set.</param>
        /// <returns>Returns the texture for chaining.</returns>
        [return: NotNullIfNotNull(nameof(texture))]
        public Texture2D? SetName(string assetName)
        {
            texture?.Name = assetName;

            return texture;
        }
    }
}
