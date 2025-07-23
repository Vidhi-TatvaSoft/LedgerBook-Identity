using BusinessAcessLayer.Interface;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using LedgerBook.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace LedgerBook.Controllers;

public class ActivityLogController : BaseController
{
    private readonly IBusinessService _businessService;
    private readonly IPartyService _partyService;
    private readonly IActivityLogService _activityLogService;
    public ActivityLogController(
         ILoginService loginService,
         IViewRenderService viewRenderService,
         IActivityLogService activityLogService,
         IBusinessService businessService,
         IPartyService partyService
    ) : base(loginService, viewRenderService, activityLogService)
    {
        _activityLogService = activityLogService;
        _businessService = businessService;
        _partyService = partyService;
    }

    #region activity log index page
    public IActionResult ActivityLog()
    {
        IActionResult redirectResult = RedirectToLoginIfNotAuthenticated();
        if (redirectResult != null)
        {
            return redirectResult;
        }
        ActivityDataViewModel activityDataVM = new()
        {
            EntityType = "-1",
            BusinessId = 0,
            SubEntityType = "-1",
            PartyId = 0,
            ActionName = "-1",
            PageNumber = 1,
            PageSize = 10
        };

        return View(activityDataVM);
    }
    #endregion

    #region get activities
    public IActionResult GetActivities(string activityData)
    {
        ActivityDataViewModel activityDataVM = JsonConvert.DeserializeObject<ActivityDataViewModel>(activityData);
        ApplicationUser user = GetCurrentUserIdentity();
        PaginationViewModel<ActivityLogsViewModel> activities = _activityLogService.GetActivities(activityDataVM, user.Id);
        return PartialView("_DisplayActivities", activities);
    }
    #endregion

    #region get all bsuiness of user
    public IActionResult GetAllBusiness()
    {
        ApplicationUser user = GetCurrentUserIdentity();
        List<BusinessViewModel> businesses = _businessService.GetAllBusinesses(user.Id);
        return PartialView("_BusinessDropDown", businesses);
    }
    #endregion

    #region get all parties by business
    public IActionResult GetAllParties(int businessId)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        List<Parties> parties = _partyService.GetAllPartiesByBusiness(businessId, user.Id);
        return PartialView("_PartiesDropDown", parties);
    }
    #endregion
}
