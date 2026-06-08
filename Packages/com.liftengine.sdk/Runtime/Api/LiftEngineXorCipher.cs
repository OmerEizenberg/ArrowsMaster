using System;
using System.Text;

namespace LiftEngine.Api
{
    /// <summary>
    /// Mirrors backend xorEncrypt: UTC daily key (DDMMYYYY) + XOR + base64.
    /// XOR is symmetric — decrypt uses the same transform as encrypt.
    /// </summary>
    internal static class LiftEngineXorCipher
    {
        public static string GetDailyKey()
        {
            var now = DateTime.UtcNow;
            return $"{now.Day:00}{now.Month:00}{now.Year:0000}";
        }

        public static string XorTransform(string base64CipherText)
        {
            var xored = Convert.FromBase64String(base64CipherText);
            var key = GetDailyKey();
            var jsonBuf = new byte[xored.Length];

            for (var i = 0; i < xored.Length; i++)
                jsonBuf[i] = (byte)(xored[i] ^ key[i % key.Length]);

            return Encoding.UTF8.GetString(jsonBuf);
        }

        /// <summary>
        /// Decode predict response body to JSON text. Supports encrypted base64 and legacy plain JSON.
        /// </summary>
        public static bool TryDecodePredictResponse(string body, out string json)
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
                    // fall through to base64 decode
                }
            }

            try
            {
                var decrypted = XorTransform(trimmed);
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
