using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Dashboard.GetDashboard;
using SchoMans.Application.Features.Users.GetMe;
using SchoMans.Application.Features.Users.SyncUser;

namespace SchoMans.Application.Tests.Common;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_every_handler_as_a_plain_class()
    {
        var services = new ServiceCollection().AddApplication();

        foreach (var handler in new[] { typeof(GetDashboardHandler), typeof(GetMeHandler), typeof(SyncUserHandler) })
        {
            Assert.Contains(services, d => d.ServiceType == handler && d.ImplementationType == handler);
        }
    }

    [Fact]
    public void AddApplication_registers_the_validators()
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider();

        Assert.IsType<SyncUserValidator>(provider.GetRequiredService<IValidator<SyncUserCommand>>());
    }
}
