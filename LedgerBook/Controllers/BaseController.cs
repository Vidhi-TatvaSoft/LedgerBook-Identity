using BusinessAcessLayer.Constant;
using BusinessAcessLayer.Interface;
using DataAccessLayer.Constant;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Mvc;

namespace LedgerBook.Controllers;

public class BaseController : Controller
{
    protected readonly ILoginService _loginService;
    private readonly IViewRenderService _viewRenderService;
    private readonly IActivityLogService _activityLogService;

    protected BaseController(ILoginService loginService, IActivityLogService activityLogService)
    {
        _loginService = loginService;
        _activityLogService = activityLogService;
    }

    protected BaseController(ILoginService loginService,
        IViewRenderService viewRenderService, IActivityLogService activityLogService)
    {
        _loginService = loginService;
        _viewRenderService = viewRenderService;
        _activityLogService = activityLogService;
    }

    #region get current user from token
    protected User GetCurrentUser()
    {
        string token = Request.Cookies[TokenKey.UserToken];
        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("User is not authenticated. Please log in.");
        }
        User user = _loginService.GetUserFromToken(token);
        if (user == null)
        {
            return null;
            throw new Exception("Invalid token or user not found.");
        }
        return user;
    }
    #endregion

    #region get current user from token
    protected ApplicationUser GetCurrentUserIdentity()
    {
        string token = Request.Cookies[TokenKey.UserToken];
        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("User is not authenticated. Please log in.");
        }
        ApplicationUser user = _loginService.GetUserFromTokenIdentity(token);
        if (user == null)
        {
            return null;
            throw new Exception("Invalid token or user not found.");
        }
        return user;
    }
    #endregion

    #region render to login page if not authorized
    protected IActionResult RedirectToLoginIfNotAuthenticated()
    {
        if (Request.Cookies[TokenKey.UserToken] == null)
        {
            return RedirectToAction("Login", "Login");
        }
        return null;
    }
    #endregion

    #region partial view response
    protected async Task<ViewResponseModel> PartialViewResponse(string viewName, object model, string message = null, ErrorType errorType = ErrorType.Success)
    {
        ViewResponseModel partialViewResponseModel = new();
        partialViewResponseModel.Message = message;
        partialViewResponseModel.ErrorType = errorType;
        partialViewResponseModel.HTML = await _viewRenderService.RenderToStringAsync($"Views/{viewName}", model);
        return partialViewResponseModel;
    }
    #endregion

    #region set activity log
    protected async Task<ActivityLogs> SetActivityLog(string message, EnumHelper.Actiontype action, EnumHelper.ActivityEntityType entityType, int EntityTypeId, int? createdById = null, EnumHelper.ActivityEntityType? subEntityType = null, int? subEntityTypeId = null)
    {
        return await _activityLogService.SetActivityLog(message, action, entityType, EntityTypeId, createdById, subEntityType, subEntityTypeId);
    }
    #endregion
}