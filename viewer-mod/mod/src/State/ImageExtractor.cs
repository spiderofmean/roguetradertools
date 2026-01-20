using System;
using UnityEngine;

namespace ViewerMod.State
{
    /// <summary>
    /// Extracts image data from Unity texture and sprite objects.
    /// </summary>
    public static class ImageExtractor
    {
        /// <summary>
        /// Extracts PNG image bytes from a handle that references a Texture2D or Sprite.
        /// </summary>
        public static byte[] ExtractImage(HandleRegistry registry, Guid handleId)
        {
            if (!registry.TryGet(handleId, out var obj))
            {
                throw new ArgumentException($"Handle not found: {handleId}");
            }

            if (obj == null)
            {
                throw new ArgumentException("Handle references null object");
            }

            Texture2D texture = null;

            // Check if it's a Texture2D
            if (obj is Texture2D tex2d)
            {
                texture = tex2d;
            }
            // Check if it's a Sprite
            else if (obj is Sprite sprite)
            {
                texture = GetTextureFromSprite(sprite);
            }
            // Check if it's a Texture (base class)
            else if (obj is Texture tex)
            {
                texture = ConvertToTexture2D(tex);
            }
            else
            {
                throw new ArgumentException($"Object is not a texture type: {obj.GetType().FullName}");
            }

            if (texture == null)
            {
                throw new InvalidOperationException("Could not extract texture");
            }

            return EncodeTextureToPng(texture);
        }

        private static Texture2D GetTextureFromSprite(Sprite sprite)
        {
            if (sprite == null) return null;

            var texture = sprite.texture;
            if (texture == null) return null;

            // If the sprite uses the full texture, return it directly
            var rect = sprite.textureRect;
            if (rect.x == 0 && rect.y == 0 && 
                Mathf.Approximately(rect.width, texture.width) && 
                Mathf.Approximately(rect.height, texture.height))
            {
                return MakeReadable(texture);
            }

            // Otherwise, extract the sprite's region
            return ExtractSpriteRegion(sprite);
        }

        private static Texture2D ExtractSpriteRegion(Sprite sprite)
        {
            var sourceTexture = sprite.texture;
            var rect = sprite.textureRect;

            // Make source readable
            var readable = MakeReadable(sourceTexture);
            if (readable == null) return null;

            // Create new texture for the sprite region
            var width = (int)rect.width;
            var height = (int)rect.height;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                var pixels = readable.GetPixels((int)rect.x, (int)rect.y, width, height);
                result.SetPixels(pixels);
                result.Apply();
                return result;
            }
            catch (Exception ex)
            {
                Entry.LogError($"Error extracting sprite region: {ex.Message}");
                // Fall back to full texture
                return readable;
            }
        }

        private static Texture2D ConvertToTexture2D(Texture texture)
        {
            if (texture == null) return null;

            // Create a temporary RenderTexture
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            
            try
            {
                // Blit the texture to the render texture
                Graphics.Blit(texture, rt);

                // Read pixels from the render texture
                var previous = RenderTexture.active;
                RenderTexture.active = rt;

                var result = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                result.Apply();

                RenderTexture.active = previous;
                return result;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D MakeReadable(Texture2D texture)
        {
            if (texture == null) return null;

            // Check if already readable
            if (texture.isReadable)
            {
                return texture;
            }

            // Use RenderTexture to make a readable copy
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            
            try
            {
                Graphics.Blit(texture, rt);

                var previous = RenderTexture.active;
                RenderTexture.active = rt;

                var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();

                RenderTexture.active = previous;
                return readable;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static byte[] EncodeTextureToPng(Texture2D texture)
        {
            // Make sure the texture is readable
            var readable = MakeReadable(texture);
            if (readable == null)
            {
                throw new InvalidOperationException("Could not make texture readable");
            }

            try
            {
                return ImageConversion.EncodeToPNG(readable);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to encode texture to PNG: {ex.Message}");
            }
        }
    }
}
