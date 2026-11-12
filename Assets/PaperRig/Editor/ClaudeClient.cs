using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace PaperRig
{
    public static class ClaudeClient
    {
        const string ApiUrl = "https://api.anthropic.com/v1/messages";
        const string Model = "claude-haiku-5-5";
        const string ApiKeyPref = "PaperRig.ApiKey";

        static readonly HttpClient http = new HttpClient();

        public static string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyPref, "");
            set => EditorPrefs.SetString(ApiKeyPref, value);
        }

        [System.Serializable] public class ApiMessage { public string role; public string content; }
        [System.Serializable] public class ApiThinking { public string type = "disabled"; }
        [System.Serializable] public class ApiRequest { public string model; public int max_tokens; public ApiMessage[] messages; public ApiThinking thinking = new ApiThinking(); }
        [System.Serializable] public class ApiBlock { public string type; public string text; }
        [System.Serializable] public class ApiResponse { public ApiBlock[] content; public string stop_reason; }

        public static async Task<string> AskAsync(string prompt)
        {
            var request = new ApiRequest
            {
                model = Model,
                max_tokens = 8000,
                messages = new[] { new ApiMessage { role = "user", content = prompt } }
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            message.Headers.Add("x-api-key", ApiKey);
            message.Headers.Add("anthropic-version", "2023-06-01");
            message.Content = new StringContent(JsonUtility.ToJson(request), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await http.SendAsync(message);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new System.Exception($"AIの呼び出しに失敗しました（{(int)response.StatusCode}）\n{body}");
            }

            ApiResponse parsed = JsonUtility.FromJson<ApiResponse>(body);
            if (parsed.content != null)
            {
                foreach (ApiBlock block in parsed.content)
                {
                    if (block.type == "text") return block.text;
                }
            }
            throw new System.Exception($"AIの返事に文章がありませんでした（stop_reason: {parsed.stop_reason}）");
        }

        public static string ExtractJson(string text)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end < start) throw new System.Exception("AIの返事を読み取れませんでした:\n" + text);
            return text.Substring(start, end - start + 1);
        }
    }
}