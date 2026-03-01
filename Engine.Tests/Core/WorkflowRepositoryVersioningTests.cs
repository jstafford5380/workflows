using Engine.Core.Definitions;
using Engine.Persistence;
using Engine.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Engine.Tests.Core;

public sealed class WorkflowRepositoryVersioningTests
{
    [Fact]
    public async Task RegisterDefinition_ShouldStartAtRevisionOne_AndIncrementOnReplacement()
    {
        await using var db = CreateDbContext();
        var repository = new WorkflowRepository(db);

        var first = await repository.RegisterDefinitionAsync(CreateDefinition("provision", 1), CancellationToken.None);
        var replacement = await repository.RegisterDefinitionAsync(CreateDefinition("provision", 1), CancellationToken.None);

        Assert.Equal(1, first.Revision);
        Assert.Equal(2, replacement.Revision);

        var definitions = await repository.ListDefinitionsAsync(CancellationToken.None);
        var latest = Assert.Single(definitions, x => x.Name == "provision" && x.Version == 1);
        Assert.Equal(2, latest.Revision);
    }

    [Fact]
    public async Task RegisterDefinition_ShouldRequireSequentialVersionBumps()
    {
        await using var db = CreateDbContext();
        var repository = new WorkflowRepository(db);

        await repository.RegisterDefinitionAsync(CreateDefinition("provision", 1), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.RegisterDefinitionAsync(CreateDefinition("provision", 3), CancellationToken.None));

        Assert.Contains("must be 1 (replace current) or 2 (next version)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterDefinition_ShouldRequireVersionOneForFirstRegistration()
    {
        await using var db = CreateDbContext();
        var repository = new WorkflowRepository(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.RegisterDefinitionAsync(CreateDefinition("provision", 2), CancellationToken.None));

        Assert.Contains("must start at version 1", ex.Message, StringComparison.Ordinal);
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"workflow-repo-tests-{Guid.NewGuid():N}")
            .Options;

        return new WorkflowDbContext(options);
    }

    private static WorkflowDefinition CreateDefinition(string name, int version)
    {
        return new WorkflowDefinition
        {
            Name = name,
            Version = version,
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "step-1",
                    DisplayName = "Step 1",
                    ActivityRef = "local.echo"
                }
            ]
        };
    }
}
