namespace EasyTransport.Common.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EasyTransport.Common.Helpers;

    /// <summary>
    /// A set of helper methods as extensions on <see cref="System.Threading.Tasks"/>
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// Returns a <see><cref>IEnumerable{Task{T}}</cref></see> which is sorted by the order
        /// each of the <paramref name="tasks"/> is completed.
        /// </summary>
        /// <remarks>
        /// The problem is that you don’t know in what order the Tasks will complete; hence WhenAny. However if you
        /// were to return not the actual Tasks, but a series of Tasks that represent the result of the first Task to complete,
        /// and then the second Task to complete, et cetera, that would suffice. To achieve this you will create as many
        /// TaskCompletionSources as there are Tasks. Each real Task would then have a single continuation registered; the
        /// responsibility of the first continuation to actually run is to set the result of the first TaskCompletionSource.Task to
        /// the outcome of the antecedent Task. Each subsequent continuation sets the next TaskCompletionSource result. Last,
        /// the method returns the Tasks for each of the TaskCompletionSources, to be consumed by the caller using a for-each.
        /// </remarks>
        /// <typeparam name="T">Type of the result returned by the <paramref name="tasks"/></typeparam>
        /// <param name="tasks">The tasks to be returned in order of completion</param>
        /// <returns>The tasks returned in the order of completion</returns>
        internal static IEnumerable<Task<T>> OrderByCompletion<T>(this IEnumerable<Task<T>> tasks)
        {
            var allTasks = Ensure.NotNull(tasks, nameof(tasks)).ToList();
            Ensure.That(allTasks.Count > 0);

            var taskCompletionSources = new TaskCompletionSource<T>[allTasks.Count];

            var nextCompletedTask = -1;
            for (var nTask = 0; nTask < allTasks.Count; nTask++)
            {
                taskCompletionSources[nTask] = new TaskCompletionSource<T>();
                allTasks[nTask].ContinueWith(t =>
                {
                    var taskToComplete = Interlocked.Increment(ref nextCompletedTask);
                    switch (t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            taskCompletionSources[taskToComplete].SetResult(t.Result);
                            break;

                        case TaskStatus.Faulted:
                            taskCompletionSources[taskToComplete].SetException(t.Exception);
                            break;

                        case TaskStatus.Canceled:
                            taskCompletionSources[taskToComplete].SetCanceled();
                            break;

                        default:
                            throw new InvalidOperationException("Invalid state: " + t.Status);
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            return taskCompletionSources.Select(t => t.Task);
        }

        /// <summary>
        /// Executes the given action on each of the tasks in turn, in the order of
        /// the sequence. The action is passed the result of each task.
        /// </summary>
        internal static async Task ForEach<T>(this IEnumerable<Task<T>> tasks, Action<T> action)
        {
            Ensure.NotNull(tasks, nameof(tasks));
            Ensure.NotNull(action, nameof(action));

            foreach (var task in tasks)
            {
                var value = await task;
                action(value);
            }
        }

        /// <summary>
        /// Returns a <see cref="Task"/> that is deemed to have completed when
        /// all the <paramref name="tasks"/> have completed. Completed could mean
        /// <c>Faulted</c>, <c>Canceled</c> or <c>RanToCompletion</c>.
        /// </summary>
        /// <remarks>
        /// <c>Task.WhenAll</c> method keeps you unaware of the outcome of all the tasks 
        /// until the final one has completed. With this method you can stop waiting if 
        /// any of the supplied <paramref name="tasks"/> fails or cancels.
        /// </remarks>
        /// <typeparam name="T">Type of the result returned by the <paramref name="tasks"/></typeparam>
        /// <param name="tasks">The tasks to wait on.</param>
        /// <returns>A task returning all the results intended to be returned by <paramref name="tasks"/></returns>
        internal static Task<T[]> WhenAllOrFail<T>(this IEnumerable<Task<T>> tasks)
        {
            var allTasks = Ensure.NotNull(tasks, nameof(tasks)).ToList();
            Ensure.That(allTasks.Count > 0);

            var tcs = new TaskCompletionSource<T[]>();

            var taskCompletedCount = 0;
            Action<Task<T>> completeAction = t =>
            {
                if (t.IsFaulted)
                {
                    tcs.TrySetException(t.Exception);
                    return;
                }

                if (t.IsCanceled)
                {
                    tcs.TrySetCanceled();
                    return;
                }

                if (Interlocked.Increment(ref taskCompletedCount) == allTasks.Count)
                {
                    tcs.SetResult(allTasks.Select(ct => ct.Result).ToArray());
                }
            };

            allTasks.ForEach(t => t.ContinueWith(completeAction));

            return tcs.Task;
        }

        #region Exception Handling

        /// <summary>
        /// Suppresses default exception handling of a Task that would otherwise re-raise 
        /// the exception on the finalizer thread.
        /// </summary>
        /// <param name="task">The Task to be monitored</param>
        /// <returns>The original Task</returns>
        internal static Task IgnoreExceptions(this Task task)
        {
            Ensure.NotNull(task, nameof(task));

            return task.ContinueWith(t =>
            {
                var ignored = t.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        }

        /// <summary>
        /// Suppresses default exception handling of a Task that would otherwise re-raise 
        /// the exception on the finalizer thread.
        /// </summary>
        /// <param name="task">The Task to be monitored</param>
        /// <returns>The original Task</returns>
        internal static Task<T> IgnoreExceptions<T>(this Task<T> task)
        {
            Ensure.NotNull(task, nameof(task));
            return (Task<T>)((Task)task).IgnoreExceptions();
        }

        /// <summary>
        /// Handles all the exceptions thrown by the <paramref name="task"/>.
        /// </summary>
        /// <param name="task">The task which might throw exceptions</param>
        /// <param name="exceptionsHandler">The handler to which every exception is passed</param>
        /// <returns>The continuation task added to the <paramref name="task"/></returns>
        internal static Task HandleExceptions(this Task task, Action<Exception> exceptionsHandler)
        {
            Ensure.NotNull(task, nameof(task));
            Ensure.NotNull(exceptionsHandler, nameof(exceptionsHandler));

            return task.ContinueWith(t =>
            {
                var e = t.Exception;

                if (e == null) { return; }

                e.Flatten().Handle(ie =>
                {
                    exceptionsHandler(ie);
                    return true;
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        }

        /// <summary>
        /// Handles expected exception(s) thrown by the <paramref name="task"/> which are specified by <paramref name="exceptionPredicate"/>.
        /// </summary>
        /// <param name="task">The task which might throw exceptions.</param>
        /// <param name="exceptionPredicate">The predicate specifying which exception(s) to handle</param>
        /// <param name="exceptionHandler">The handler to which every exception is passed</param>
        /// <returns>The continuation task added to the <paramref name="task"/></returns>
        internal static Task HandleExceptions(this Task task, Func<Exception, bool> exceptionPredicate, Action<Exception> exceptionHandler)
        {
            Ensure.NotNull(task, nameof(task));
            Ensure.NotNull(exceptionPredicate, nameof(exceptionPredicate));
            Ensure.NotNull(exceptionHandler, nameof(exceptionHandler));

            return task.ContinueWith(t =>
            {
                var e = t.Exception;

                if (e == null) { return; }

                e.Flatten().Handle(ie =>
                {
                    if (exceptionPredicate(ie))
                    {
                        exceptionHandler(ie);
                        return true;
                    }

                    return false;
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        }

        /// <summary>
        /// Handles an expected exception thrown by the <paramref name="task"/>.
        /// </summary>
        /// <typeparam name="T">Type of exception to handle</typeparam>
        /// <param name="task">The task which might throw exceptions</param>
        /// <param name="exceptionHandler">The handler to which every exception is passed</param>
        /// <returns>The continuation task added to the <paramref name="task"/></returns>
        internal static Task HandleException<T>(this Task task, Action<T> exceptionHandler) where T : Exception
        {
            Ensure.NotNull(task, nameof(task));
            Ensure.NotNull(exceptionHandler, nameof(exceptionHandler));

            return task.ContinueWith(t =>
            {
                var e = t.Exception;

                if (e == null) { return; }

                e.Flatten().Handle(ie =>
                {
                    if (ie.GetType() == typeof(T))
                    {
                        exceptionHandler((T)ie);
                        return true;
                    }

                    return false;
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        }

        #endregion
    }
}