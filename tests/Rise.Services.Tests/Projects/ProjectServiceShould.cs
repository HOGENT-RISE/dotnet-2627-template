using System.Security.Claims;
using Rise.Domain.Projects;
using Rise.Services.Projects;
using Rise.Services.Tests.Fakers;
using Rise.Shared.Projects;

namespace Rise.Services.Tests.Projects;

public class ProjectServiceShould(DatabaseFixture database, ITestOutputHelper output) : DatabaseTest(database, output)
{
    [Fact]
    public async Task EditProjectWhenTechnicianIsTheSame()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var technicianAccountId = Guid.NewGuid().ToString();
        var technician = new Technician("FName", "LName", technicianAccountId);
        var project = new Project(
            "Old Project",
            technician,
            new Address("Street", "Bus", "City", "Zip"));

        dbContext.Technicians.Add(technician);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        Log.Information("Done Arranging");

        var claimsPrincipal = CreateClaimsPrincipal(technicianAccountId);
        var sessionProvider = new FakeSessionContextProvider(claimsPrincipal);
        var service = new ProjectService(dbContext, sessionProvider);

        var request = new ProjectRequest.Edit
        {
            ProjectId = project.Id,
            Name = "New Project"
        };

        Log.Information("Acting");
        var result = await service.EditAsync(request);

        Log.Information("Asserting");
        result.IsSuccess.ShouldBeTrue();

        await using var verificationContext = CreateDbContext();

        var updatedProject = await verificationContext.Projects
            .SingleAsync(p => p.Id == project.Id);

        updatedProject.Name.ShouldBe("New Project");
    }

    [Fact]
    public async Task NotEditProjectWhenTechnicianIsDifferent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var correctTechAccountId = Guid.NewGuid().ToString();
        var otherTechAccountId = Guid.NewGuid().ToString();

        var projectTechnician = new Technician(
            "FName",
            "LName",
            correctTechAccountId);

        var loggedInTechnician = new Technician(
            "Other",
            "Tech",
            otherTechAccountId);

        var project = new Project(
            "Old Project",
            projectTechnician,
            new Address("Street", "Bus", "City", "Zip"));

        dbContext.Technicians.AddRange(projectTechnician, loggedInTechnician);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var claimsPrincipal = CreateClaimsPrincipal(otherTechAccountId);
        var sessionProvider = new FakeSessionContextProvider(claimsPrincipal);
        var service = new ProjectService(dbContext, sessionProvider);

        var request = new ProjectRequest.Edit
        {
            ProjectId = project.Id,
            Name = "New Project"
        };

        // Act
        var result = await service.EditAsync(request);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Unauthorized);

        await using var verificationContext = CreateDbContext();

        var unchangedProject = await verificationContext.Projects
            .SingleAsync(p => p.Id == project.Id);

        unchangedProject.Name.ShouldBe("Old Project");
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string accountId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, accountId)
        ], "mock"));
    }
}