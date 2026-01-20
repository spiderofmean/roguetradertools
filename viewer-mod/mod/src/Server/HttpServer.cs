using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ViewerMod.Server
{
    /// <summary>
    /// HTTP server using System.Net.HttpListener for handling API requests.
    /// </summary>
    public class HttpServer
    {
        private readonly int _port;
        private readonly ViewerBehaviour _behaviour;
        private readonly HttpListener _listener;
        private readonly Router _router;
        private CancellationTokenSource _cts;
        private Task _listenerTask;

        public HttpServer(int port, ViewerBehaviour behaviour)
        {
            _port = port;
            _behaviour = behaviour;
            _listener = new HttpListener();
            _router = new Router(behaviour);
        }

        public void Start()
        {
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenLoop(_cts.Token));

            Entry.Log($"HTTP server listening on http://localhost:{_port}/");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    // Expected when stopping
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener was disposed
                    break;
                }
                catch (Exception ex)
                {
                    Entry.LogError($"Error in listener loop: {ex}");
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Add CORS headers for web client
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                // Handle preflight requests
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                var path = request.Url.AbsolutePath.ToLowerInvariant();
                var method = request.HttpMethod.ToUpperInvariant();

                Entry.Log($"Request: {method} {path}");

                await _router.RouteAsync(request, response);
            }
            catch (Exception ex)
            {
                Entry.LogError($"Error handling request: {ex}");
                await SendJsonErrorAsync(response, 500, $"Internal server error: {ex.Message}");
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch { }
            }
        }

        private async Task SendJsonErrorAsync(HttpListenerResponse response, int statusCode, string message)
        {
            try
            {
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                var json = JsonConvert.SerializeObject(new { error = message });
                var bytes = Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch { }
        }
    }
}
