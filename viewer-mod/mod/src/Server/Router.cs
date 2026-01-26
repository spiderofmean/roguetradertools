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
    public class Router
    {
        private readonly ViewerBehaviour _behaviour;
        private readonly HandleRegistry _registry;
        private readonly ObjectInspector _inspector;
        private readonly BlueprintService _blueprintService;

        public Router(ViewerBehaviour behaviour)
        {
            _behaviour = behaviour;
            _registry = HandleRegistry.Instance;
            _inspector = new ObjectInspector(_registry);
            _blueprintService = new BlueprintService();
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
                else if (path == "/api/blueprints" && method == "GET")
                {
                    await HandleBlueprintsListAsync(request, response);
                }
                else if (path == "/api/blueprints/range" && method == "POST")
                {
                    await HandleBlueprintsRangeAsync(request, response);
                }
                else if (path == "/api/blueprints/equipment/stream" && method == "POST")
                {
                    await HandleBlueprintsEquipmentStreamAsync(request, response);
                }
                else if (path.StartsWith("/api/blueprints/equipment/icon/") && method == "GET")
                {
                    var guid = path.Substring("/api/blueprints/equipment/icon/".Length);
                    await HandleEquipmentIconAsync(guid, request, response);
                }
                else if (path.StartsWith("/api/blueprints/") && method == "GET")
                {
                    await HandleBlueprintDetailAsync(path, request, response);
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

        private async Task HandleBlueprintsListAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var blueprints = _behaviour.ExecuteOnMainThread(() => _blueprintService.GetAllBlueprints());
            await SendJsonAsync(response, 200, new { blueprints }, Formatting.None);
        }

        private async Task HandleBlueprintDetailAsync(string path, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = path.Substring("/api/blueprints/".Length);
            var result = _behaviour.ExecuteOnMainThread(() => _blueprintService.GetBlueprintByGuid(guid));

            if (result == null)
            {
                await SendJsonAsync(response, 404, new { error = "Blueprint not found" });
                return;
            }

            await SendJsonAsync(response, 200, result, Formatting.None);
        }

        private async Task HandleBlueprintsRangeAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var json = JObject.Parse(body);
            var startToken = json["start"];
            var countToken = json["count"];

            if (startToken == null || countToken == null)
            {
                await SendJsonAsync(response, 400, new { error = "Missing start/count" }, Formatting.None);
                return;
            }

            int start;
            int count;
            try
            {
                start = startToken.Value<int>();
                count = countToken.Value<int>();
            }
            catch
            {
                await SendJsonAsync(response, 400, new { error = "Invalid start/count" }, Formatting.None);
                return;
            }

            var result = _behaviour.ExecuteOnMainThread(() => _blueprintService.GetBlueprintRange(start, count));
            await SendJsonAsync(response, 200, result, Formatting.None);
        }

        private async Task HandleBlueprintsEquipmentStreamAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var json = JObject.Parse(body);
            var startToken = json["start"];
            var countToken = json["count"];

            if (startToken == null || countToken == null)
            {
                await SendJsonAsync(response, 400, new { error = "Missing start/count" }, Formatting.None);
                return;
            }

            int start;
            int count;
            try
            {
                start = startToken.Value<int>();
                count = countToken.Value<int>();
            }
            catch
            {
                await SendJsonAsync(response, 400, new { error = "Invalid start/count" }, Formatting.None);
                return;
            }

            response.StatusCode = 200;
            response.ContentType = "application/x-ndjson";

            _behaviour.ExecuteOnMainThread(() =>
            {
                _blueprintService.WriteEquipmentBlueprintsNdjson(start, count, response.OutputStream);
            });
        }

        private async Task HandleEquipmentIconAsync(string guid, HttpListenerRequest request, HttpListenerResponse response)
        {
            guid = (guid ?? "").Trim();
            guid = guid.Trim('/');
            if (guid.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                guid = guid.Substring(0, guid.Length - 4);
                guid = guid.Trim('/');
            }

            if (string.IsNullOrEmpty(guid))
            {
                await SendJsonAsync(response, 400, new { error = "Missing guid" }, Formatting.None);
                return;
            }

            byte[] imageBytes = null;
            string errorMessage = null;

            _behaviour.ExecuteOnMainThread(() =>
            {
                try
                {
                    var bp = _blueprintService.GetBlueprintObjectByGuid(guid, out var meta);
                    if (bp == null)
                    {
                        errorMessage = "Blueprint not found";
                        return;
                    }

                    var resolution = IconResolver.FindIconResolution(bp);
                    var iconObj = resolution?.Icon;
                    if (iconObj == null)
                    {
                        errorMessage = "Icon not found on blueprint";
                        return;
                    }

                    // Register the icon object so we can reuse ImageExtractor via the handle registry.
                    var handleId = _registry.Register(iconObj);
                    imageBytes = ImageExtractor.ExtractImage(_registry, handleId);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }
            });

            if (errorMessage != null)
            {
                // Differentiate not-found vs extraction errors.
                var status = errorMessage == "Blueprint not found" || errorMessage == "Icon not found on blueprint" ? 404 : 400;
                await SendJsonAsync(response, status, new { error = errorMessage }, Formatting.None);
                return;
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                await SendJsonAsync(response, 404, new { error = "Could not extract icon image" }, Formatting.None);
                return;
            }

            response.StatusCode = 200;
            response.ContentType = "image/png";
            response.ContentLength64 = imageBytes.Length;
            await response.OutputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
        }

        private async Task SendJsonAsync(HttpListenerResponse response, int statusCode, object data, Formatting formatting = Formatting.Indented)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(data, formatting, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
