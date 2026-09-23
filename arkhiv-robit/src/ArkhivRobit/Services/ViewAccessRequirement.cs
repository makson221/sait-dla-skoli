using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ArkhivRobit.Services;

/// <summary>Вимога для перегляду архіву: або архів публічний, або користувач увійшов за будь-яким кодом.</summary>
public class ViewAccessRequirement : IAuthorizationRequirement { }

public class ViewAccessHandler : AuthorizationHandler<ViewAccessRequirement>
{
    private readonly ArchiveOptions _options;

    public ViewAccessHandler(IOptions<ArchiveOptions> options) => _options = options.Value;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ViewAccessRequirement requirement)
    {
        if (_options.IsPublic || context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
