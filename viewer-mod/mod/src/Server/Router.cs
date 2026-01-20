using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ViewerMod.State;

namespace ViewerMod.Server
{
    /// <summary>
    /// Routes HTTP requests to the appropriate handlers.
    /// </summary>
    public class Router
    {
        private readonly ViewerBehaviour _behaviour;
        private readonly HandleRegistry _registry;
        private readonly ObjectInspector _inspector;

        public Router(ViewerBehaviour behaviour)
        {
            _behaviour = behaviour;
            _registry = HandleRegistry.Instance;
            _inspector = new ObjectInspector(_registry);
        }

        public async Task RouteAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var path = request.Url.AbsolutePath.ToLowerInvariant();
            var method = request.HttpMethod.ToUpperInvariant();

            try
            {
                // Route based on path
                if (path == "/api/roots" && method == "POST")
                {
                    await HandleRootsAsync(request, response);
                }
                else if (path == "/api/inspect" && method == "POST")
                {
                    await HandleInspectAsync(request, response);
                }
                else if (path == "/api/handles/clear" && method == "POST")
                {
                    await HandleClearHandlesAsync(request, response);
                }
                else if (path.StartsWith("/api/image/") && method == "GET")
                {
                    var handleIdStr = path.Substring("/api/image/".Length);
                    await HandleImageAsync(handleIdStr, request, response);
                }
                else
                {
                    await SendJsonAsync(response, 404, new { error = "Not found", path = path });
                }
            }
            catch (Exception ex)
            {
                Entry.LogError($"Router error: {ex}");
                await SendJsonAsync(response, 500, new { error = ex.Message });
            }
        }

        private async Task HandleRootsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Get roots on main thread
            var roots = _behaviour.ExecuteOnMainThread(() => RootProvider.GetRoots(_registry));
            await SendJsonAsync(response, 200, roots);
        }

        private async Task HandleInspectAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Read request body
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var json = JObject.Parse(body);
            var handleIdStr = json["handleId"]?.ToString();

            if (string.IsNullOrEmpty(handleIdStr) || !Guid.TryParse(handleIdStr, out var handleId))
            {
                await SendJsonAsync(response, 400, new { error = "Invalid or missing handleId" });
                return;
            }

            // Inspect on main thread
            var result = _behaviour.ExecuteOnMainThread(() => _inspector.Inspect(handleId));

            if (result == null)
            {
                await SendJsonAsync(response, 404, new { error = "Handle not found", handleId = handleIdStr });
                return;
            }

            await SendJsonAsync(response, 200, result);
        }

        private async Task HandleClearHandlesAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            _behaviour.ExecuteOnMainThread(() => _registry.Clear());
            await SendJsonAsync(response, 200, new { cleared = true });
        }

        private async Task HandleImageAsync(string handleIdStr, HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!Guid.TryParse(handleIdStr, out var handleId))
            {
                await SendJsonAsync(response, 400, new { error = "Invalid handleId format" });
                return;
            }

            // Get image bytes on main thread
            byte[] imageBytes = null;
            string errorMessage = null;

            _behaviour.ExecuteOnMainThread(() =>
            {
                try
                {
                    imageBytes = ImageExtractor.ExtractImage(_registry, handleId);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }
            });

            if (errorMessage != null)
            {
                await SendJsonAsync(response, 400, new { error = errorMessage });
                return;
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                await SendJsonAsync(response, 404, new { error = "Could not extract image from handle" });
                return;
            }

            response.StatusCode = 200;
            response.ContentType = "image/png";
            response.ContentLength64 = imageBytes.Length;
            await response.OutputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
        }

        private async Task SendJsonAsync(HttpListenerResponse response, int statusCode, object data)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
