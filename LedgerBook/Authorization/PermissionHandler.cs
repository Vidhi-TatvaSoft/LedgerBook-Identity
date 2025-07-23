using BusinessAcessLayer.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using BusinessAcessLayer.Constant;
namespace LedgerBook.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{

    private readonly IJWTTokenService _jWTService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILoginService _loginService;
    private readonly IBusinessService _businessService;
    private readonly IUserBusinessMappingService _userBusinessMappingService;

    public PermissionHandler(IJWTTokenService jWTService,
     IHttpContextAccessor httpContextAccessor, ILoginService loginService,
      IBusinessService businessService, IUserBusinessMappingService userBusinessMappingService)
        : base()
    {
        this._jWTService = jWTService;
        this._httpContextAccessor = httpContextAccessor;
        this._loginService = loginService;
        this._businessService = businessService;
        this._userBusinessMappingService = userBusinessMappingService;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var cookieSavedToken = httpContext.Request.Cookies[TokenKey.UserToken];
        // var roles = httpContext.Request.Cookies["Roles"];
        if (string.IsNullOrEmpty(cookieSavedToken))
        {
            throw new Exception("User is not authenticated. Please log in.");
        }
        User user = _loginService.GetUserFromToken(cookieSavedToken);

        var businessToken =  httpContext.Request.Cookies[TokenKey.BusinessToken];
        if (string.IsNullOrEmpty(businessToken))
        {
            throw new Exception("Invalid token or business not found");
        }
        Businesses business = _businessService.GetBusinessFromToken(businessToken);
        List<RoleViewModel> rolesByUser = _userBusinessMappingService.GetRolesByBusinessId(business.Id, user.Id);

        if (string.IsNullOrEmpty(cookieSavedToken))
        {
            httpContext.Response.Redirect("/Login/Login");
            return Task.CompletedTask;
        }
        string email = _jWTService.GetClaimValue(cookieSavedToken, "email");
        if (string.IsNullOrEmpty(email))
        {
            httpContext.Response.Redirect("/Login/Login");
            return Task.CompletedTask;
        }
        switch (requirement.Permission)
        {
            case "Owner/Admin":
                if (rolesByUser.Any(role => role.RoleName == "Owner/Admin"))
                {
                    context.Succeed(requirement);
                }
                break;
            case "PurchaseManager":
                if (rolesByUser.Any(role => role.RoleName == "Purchase Manager" || role.RoleName == "Owner/Admin"))
                {
                    context.Succeed(requirement);
                }
                break;
            case "SalesManager":
                if (rolesByUser.Any(role => role.RoleName == "Sales Manager" || role.RoleName == "Owner/Admin"))
                {
                    context.Succeed(requirement);
                }
                break;

            default:
                break;
        }
        return Task.CompletedTask;
    }
}