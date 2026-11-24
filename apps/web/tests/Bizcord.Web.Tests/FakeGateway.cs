using System.Net;
using System.Text;
using System.Text.Json;
using Bizcord.Services;

namespace Bizcord.Web.Tests;

/// <summary>Stands in for the API gateway: hands out tokens from /Faucet and answers everything else with <see cref="Respond"/>.</summary>
internal sealed class FakeGateway : HttpMessageHandler
{
    private int _tokenNumber;

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> FaucetBodies { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json(new { });

    public GatewayClient CreateClient(GatewayTokenCache? tokens = null) =>
        new(new HttpClient(this) { BaseAddress = new Uri("http://gateway/") }, tokens ?? new GatewayTokenCache(TimeProvider.System));

    public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.RequestUri!.AbsolutePath == "/Faucet")
        {
            FaucetBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return Json(new { name = (string?)null, value = $"token-{++_tokenNumber}" });
        }

        return Respond(request);
    }
}
