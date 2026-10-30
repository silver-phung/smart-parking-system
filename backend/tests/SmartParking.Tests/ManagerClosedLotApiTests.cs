using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Data;
using Xunit;

namespace SmartParking.Tests;

public class ManagerClosedLotApiTests
{
    [Fact]
    public async Task ClosedLotDetail_RequiresActiveAssignmentForManager()
    {
        using var factory = new CustomWebApplicationFactory();
        string token;
        long assignmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
            db.ParkingLots.Single(l => l.Id == 1).IsActive = false;
            foreach (var existing in db.StaffLotAssignments.Where(a => a.UserId == 9002)) existing.IsActive = false;
            await db.SaveChangesAsync();
            var manager = db.Users.Single(u => u.Id == 9002);
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(manager, Roles.Manager);
        }
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/parking-lots/1")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/parking-lots/1")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
            var assignment = new StaffLotAssignment { UserId = 9002, LotId = 1, AssignedBy = 9001, IsActive = true, Duty = "BOTH" };
            db.StaffLotAssignments.Add(assignment);
            await db.SaveChangesAsync();
            assignmentId = assignment.Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/parking-lots/1")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
            db.StaffLotAssignments.Single(a => a.Id == assignmentId).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/parking-lots/1")).StatusCode);
    }
}
