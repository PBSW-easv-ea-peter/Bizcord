using ChatService.Application;
using ChatService.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Tests.Api;

/// <summary>The consumer resolves IMessageHandler&lt;T&gt; per reply - all three must lead to the orchestrator.</summary>
[Trait("Category", "Integration")]
public class FileSagaWiringTests(ChatApiFactory factory) : IClassFixture<ChatApiFactory>
{
    [Fact]
    public void Every_saga_reply_is_handled_by_the_orchestrator()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var orchestrator = services.GetRequiredService<FileMessageSagaOrchestrator>();
        Assert.Same(orchestrator, services.GetRequiredService<IMessageHandler<QuotaReserved>>());
        Assert.Same(orchestrator, services.GetRequiredService<IMessageHandler<QuotaRejected>>());
        Assert.Same(orchestrator, services.GetRequiredService<IMessageHandler<QuotaReleased>>());
    }
}
