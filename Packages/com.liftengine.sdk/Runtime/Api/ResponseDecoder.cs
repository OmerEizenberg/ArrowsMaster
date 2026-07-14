using System;
using System.Text;

namespace LiftEngine.Api
{
    internal static class ResponseDecoder
    {
        public static string GetDailyKey()
        {
            var now = DateTime.UtcNow;
            return $"{now.Day:00}{now.Month:00}{now.Year:0000}";
        }

        public static string DecodeTransform(string encodedBody)
        {
            var encoded = Convert.FromBase64String(encodedBody);
            var key = GetDailyKey();
            var jsonBuf = new byte[encoded.Length];

            for (var i = 0; i < encoded.Length; i++)
                jsonBuf[i] = (byte)(encoded[i] ^ key[i % key.Length]);

            return Encoding.UTF8.GetString(jsonBuf);
        }

        public static bool TryDecodeResponse(string body, out string json)
        {
            json = null;
            if (string.IsNullOrWhiteSpace(body))
                return false;

            var trimmed = body.Trim();

            if (LooksLikeJson(trimmed))
            {
                json = trimmed;
                return true;
            }

            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            {
                try
                {
                    trimmed = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(trimmed);
                    if (!string.IsNullOrEmpty(trimmed) && LooksLikeJson(trimmed))
                    {
                        json = trimmed;
                        return true;
                    }
                }
                catch
                {
                    // fall through to encoded decode
                }
            }

            try
            {
                var decrypted = DecodeTransform(trimmed);
                if (!LooksLikeJson(decrypted))
                    return false;

                json = decrypted;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var c = text[0];
            return c == '[' || c == '{';
        }
    }
}
