using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScribAi.Api.Options;
using ScribAi.Api.Pipeline.Llm;
using System.Net;
using System.Text;

namespace ScribAi.Api.Tests;

public class OllamaExtractorTests
{
    private class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Requests.Add(req);
            return Task.FromResult(respond(req));
        }
    }

    private static OllamaExtractor Build(StubHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434") },
            Microsoft.Extensions.Options.Options.Create(new OllamaOptions { DefaultModel = "qwen2.5:7b-instruct", Temperature = 0 }),
            NullLogger<OllamaExtractor>.Instance);

    private const string Schema = """
    { "type":"object",
      "properties": { "name": { "type":"string" }, "total": { "type":"number" } },
      "required": ["name","total"]
    }
    """;

    [Fact]
    public async Task Returns_validated_result_on_first_attempt()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "message": { "role":"assistant", "content":"{\"name\":\"ACME\",\"total\":42.5}" },
                  "prompt_eval_count": 120,
                  "eval_count": 15
                }
            """, Encoding.UTF8, "application/json")
        });
        var ex = Build(handler);

        var result = await ex.ExtractAsync("invoice text", Schema, "qwen2.5:7b-instruct");

        Assert.True(result.Validated);
        Assert.Null(result.ValidationError);
        Assert.Equal(120, result.TokensIn);
        Assert.Equal(15, result.TokensOut);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Retries_once_on_schema_failure_then_succeeds()
    {
        var count = 0;
        var handler = new StubHandler(_ =>
        {
            count++;
            var content = count == 1
                ? "{\"message\":{\"content\":\"{\\\"name\\\":\\\"ACME\\\"}\"}}"
                : "{\"message\":{\"content\":\"{\\\"name\\\":\\\"ACME\\\",\\\"total\\\":99}\"}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        });
        var ex = Build(handler);

        var result = await ex.ExtractAsync("text", Schema, "qwen2.5:7b-instruct");

        Assert.True(result.Validated);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Gives_up_after_two_attempts_returning_invalid()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"message\":{\"content\":\"{\\\"oops\\\":true}\"}}",
                Encoding.UTF8, "application/json")
        });
        var ex = Build(handler);

        var result = await ex.ExtractAsync("text", Schema, "qwen2.5:7b-instruct");

        Assert.False(result.Validated);
        Assert.NotNull(result.ValidationError);
        Assert.Equal(2, handler.Requests.Count);
    }
}
