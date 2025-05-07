namespace PingListBotConsole;

public class HttpPortChecker
{
    private readonly HttpClient _httpClient;

    public HttpPortChecker(int timeoutMs)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
    }

    public async Task<bool> CheckPortAsync(string host, int port)
    {
        try
        {
            var url = $"http://{host}:{port}";
            var request = new HttpRequestMessage(HttpMethod.Head, url);

            var response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}