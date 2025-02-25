// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using osu.Framework.Development;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Platform
{
    /// <summary>
    /// SDL clipboard operations need to run on the main thread.
    /// This abstract class provides an <see cref="Update"/> method that the main thread needs to call.
    /// </summary>
    public abstract class SDLClipboard : Clipboard
    {
        /// <summary>
        /// Clipboard operations will be run on a separate dedicated thread, so we need to schedule any SDL calls using this queue.
        /// </summary>
        protected ConcurrentQueue<Task> PendingActions = new ConcurrentQueue<Task>();

        /// <summary>
        /// Whether a main thread specific action can be performed inline.
        /// </summary>
        protected bool CanPerformInline =>
            ThreadSafety.IsInputThread || (ThreadSafety.ExecutionMode == ExecutionMode.SingleThread && ThreadSafety.IsUpdateThread);

        /// <summary>
        /// Enqueues an action to be performed on the main thread.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <returns>A task which can be used for continuation logic. May return a <see cref="Task.CompletedTask"/> if called while already on the main thread.</returns>
        protected Task EnqueueAction(Action action)
        {
            if (CanPerformInline)
            {
                action();
                return Task.CompletedTask;
            }

            var task = new Task(action);
            PendingActions.Enqueue(task);
            return task;
        }

        protected Task<T> EnqueueAction<T>(Func<T> action)
        {
            if (CanPerformInline)
            {
                return Task.FromResult(action());
            }

            var task = new Task<T>(action);
            PendingActions.Enqueue(task);
            return task;
        }

        public void Update()
        {
            while (PendingActions.TryDequeue(out var task))
            {
                task.RunSynchronously();

                if (task.Exception != null)
                    ExceptionDispatchInfo.Throw(task.Exception);
            }
        }
    }
}
