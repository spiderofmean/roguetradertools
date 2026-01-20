using System;
using System.Collections.Concurrent;
using UnityEngine;
using ViewerMod.Server;

namespace ViewerMod
{
    /// <summary>
    /// MonoBehaviour that runs the HTTP server and processes requests on the main thread.
    /// </summary>
    public sealed class ViewerBehaviour : MonoBehaviour
    {
        private HttpServer _server;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private void Start()
        {
            try
            {
                _server = new HttpServer(5000, this);
                _server.Start();
                Entry.Log("HTTP server started on port 5000");
            }
            catch (Exception ex)
            {
                Entry.LogError($"Failed to start HTTP server: {ex}");
            }
        }

        private void Update()
        {
            // Process queued actions on the main thread
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Entry.LogError($"Error processing queued action: {ex}");
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                _server?.Stop();
                Entry.Log("HTTP server stopped");
            }
            catch (Exception ex)
            {
                Entry.LogError($"Error stopping HTTP server: {ex}");
            }
        }

        /// <summary>
        /// Queues an action to run on the Unity main thread.
        /// </summary>
        public void QueueOnMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        /// <summary>
        /// Executes an action on the main thread and waits for completion.
        /// </summary>
        public T ExecuteOnMainThread<T>(Func<T> func)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == 1)
            {
                // Already on main thread
                return func();
            }

            T result = default;
            Exception exception = null;
            var signal = new System.Threading.ManualResetEventSlim(false);

            QueueOnMainThread(() =>
            {
                try
                {
                    result = func();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    signal.Set();
                }
            });

            signal.Wait();

            if (exception != null)
            {
                throw new Exception("Error on main thread", exception);
            }

            return result;
        }

        /// <summary>
        /// Executes an action on the main thread and waits for completion.
        /// </summary>
        public void ExecuteOnMainThread(Action action)
        {
            ExecuteOnMainThread(() =>
            {
                action();
                return true;
            });
        }
    }
}
