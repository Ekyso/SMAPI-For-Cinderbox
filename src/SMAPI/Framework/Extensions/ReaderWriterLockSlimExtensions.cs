using System;
using System.Threading;

namespace StardewModdingAPI.Framework.Extensions;

/// <summary>Provides internal extensions for <see cref="ReaderWriterLockSlim"/>.</summary>
internal static class ReaderWriterLockSlimExtensions
{
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
}
