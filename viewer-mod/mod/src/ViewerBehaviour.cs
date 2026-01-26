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
            _server = new HttpServer(5000, this);
            _server.Start();
            Entry.Log("HTTP server started on port 5000");
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                action();
            }
        }

        private void OnDestroy()
        {
            _server?.Stop();
            Entry.Log("HTTP server stopped");
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
                return func();
            }

            T result = default;
            Exception exception = null;
            var signal = new System.Threading.ManualResetEventSlim(false);

            QueueOnMainThread(() =>
            {
                try { result = func(); }
                catch (Exception ex) { exception = ex; }
                finally { signal.Set(); }
            });

            signal.Wait();
            if (exception != null) throw exception;
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
