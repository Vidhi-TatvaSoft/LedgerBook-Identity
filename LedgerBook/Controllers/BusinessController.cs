using BusinessAcessLayer.Interface;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using LedgerBook.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using DataAccessLayer.Constant;
using BusinessAcessLayer.Helper;
using BusinessAcessLayer.Constant;
using System.Threading.Tasks;
using System.Reflection.Metadata;

namespace LedgerBook.Controllers;

public class BusinessController : BaseController
{
    private readonly IBusinessService _businessService;
    private readonly IJWTTokenService _jwttokenService;
    private readonly IReferenceDataEntityService _referenceDataEntityService;
    private readonly IRoleService _roleService;
    private readonly IUserService _userService;
    private readonly IUserBusinessMappingService _userBusinessMappingService;
    private readonly ICookieService _cookieService;

    public BusinessController(IBusinessService businessService,
     IJWTTokenService jWTTokenService,
     IReferenceDataEntityService referenceDataEntity,
     ILoginService loginService, IRoleService roleService,
      IUserService userService,
      IViewRenderService viewRenderService,
      IUserBusinessMappingService userBusinessMappingService,
      IActivityLogService activityLogService,
      ICookieService cookieService) : base(loginService, viewRenderService, activityLogService)
    {
        _businessService = businessService;
        _jwttokenService = jWTTokenService;
        _referenceDataEntityService = referenceDataEntity;
        _roleService = roleService;
        _userService = userService;
        _userBusinessMappingService = userBusinessMappingService;
        _cookieService = cookieService;
    }

    #region index page
    public IActionResult Index()
    {
        _cookieService.Delete(Response, TokenKey.BusinessToken);
        _cookieService.Delete(Response, TokenKey.BusinessName);
        _cookieService.Delete(Response, TokenKey.PartyType);
        _cookieService.Delete(Response, TokenKey.BusinessId);
        _cookieService.Delete(Response, TokenKey.AllBusinesses);
        IActionResult redirectResult = RedirectToLoginIfNotAuthenticated();
        if (redirectResult != null)
        {
            return redirectResult;
        }
        ApplicationUser user = GetCurrentUserIdentity();
        if (user != null)
        {
            string username = user.FirstName + " " + user.LastName;
        }
        return View();
    }
    #endregion

    #region get all businesses
    public IActionResult GetBusinesses(string searchText = null)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
        {
            return RedirectToAction("Index", "Business");
        }
        int userId = user.Id;
        List<BusinessViewModel> businessList = _businessService.GetBusinesses(userId, searchText);
        List<BusinessViewModel> businessListRolewise = _businessService.GetRolewiseBusiness(businessList);
        return PartialView("_DisplayBusinesses", businessListRolewise);
    }
    #endregion

    #region  to render the main add edit business modal
    public async Task<IActionResult> CreateBusinessModalDisplay(int? businessId)
    {
        BusinessMainViewModel businessMainVM = new();
        // throw new Exception("Test Exception");

        businessMainVM.BusinessDetailViewModel.BusinessCategories = _referenceDataEntityService.GetReferenceValues(EnumHelper.EntityType.BusinessCategory.ToString());
        businessMainVM.BusinessDetailViewModel.BusinessTypes = _referenceDataEntityService.GetReferenceValues(EnumHelper.EntityType.BusinessType.ToString());
        businessMainVM.BusinessDetailViewModel.Roles = _roleService.GetAllRoles();
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");

        if (businessId == null)
        {
            businessMainVM.BusinessDetailViewModel.BusinessItem = new();
            businessMainVM.BusinessDetailViewModel.BusinessItem.IsActive = true;
        }
        else
        {
            businessMainVM.BusinessDetailViewModel.BusinessItem = _businessService.GetBusinessItemById(businessId);
            businessMainVM.UserPermissionViewModel.Users = await _userBusinessMappingService.GetUsersByBusiness((int)businessId, user.Id);
            businessMainVM.UserPermissionViewModel.Users = _userBusinessMappingService.SetPermissions(businessMainVM.UserPermissionViewModel.Users, user.Id, (int)businessId);
        }
        ViewBag.Categories = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessCategories, "Id", "EntityValue");
        ViewBag.Types = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessTypes, "Id", "EntityValue");
        ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");
        return PartialView("_AddBusinessDetailsPartial", businessMainVM);
    }
    #endregion

    #region  Display the business details in modal
    public IActionResult DisplayBusinessDetails(string businessVM)
    {
        BusinessMainViewModel businessMainVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessVM);
        ViewBag.Categories = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessCategories, "Id", "EntityValue");
        ViewBag.Types = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessTypes, "Id", "EntityValue");
        ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");
        return PartialView("_AddBusinessDetailsPartial", businessMainVM);
    }
    #endregion

    #region  Display user details in modal 
    public IActionResult DisplayUserDetails(string businessVM)
    {
        BusinessMainViewModel businessMainVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessVM);
        if (businessMainVM.UserPermissionViewModel.Users == null)
        {
            businessMainVM.UserPermissionViewModel.Users = new();
        }
        ViewBag.Categories = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessCategories, "Id", "EntityValue");
        ViewBag.Types = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessTypes, "Id", "EntityValue");
        ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");
        return PartialView("_AddUserDetailsPartial", businessMainVM);
    }
    #endregion

    #region create or update user modal
    [HttpGet]
    public async Task<IActionResult> RenderCreateUserModal(int? userId, int? businessId)
    {
        BusinessMainViewModel businessMainVM = new();

        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        if (userId != null)
        {
            businessMainVM.UserPermissionViewModel.Users = await _userBusinessMappingService.GetUsersByBusiness((int)businessId, user.Id);
            businessMainVM.UserPermissionViewModel.Users = _userBusinessMappingService.SetPermissions(businessMainVM.UserPermissionViewModel.Users, user.Id, (int)businessId);
            businessMainVM.UserPermissionViewModel.UserDetail = businessMainVM.UserPermissionViewModel.Users.FirstOrDefault(x => x.UserId == userId);
        }
        else
        {
            businessMainVM.UserPermissionViewModel.UserDetail = new();
        }
        bool isMainOwner = _userBusinessMappingService.IsMainOwner((int)businessId, user.Id);
        if (isMainOwner)
        {
            businessMainVM.UserPermissionViewModel.UserDetail.CanAddOwner = true;
            businessMainVM.BusinessDetailViewModel.Roles = _roleService.GetAllRoles();
        }
        else
        {
            businessMainVM.BusinessDetailViewModel.Roles = _roleService.GetRolesExceptOwner();
        }
        ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");
        return PartialView("_CreateUserModalPartial", businessMainVM);
    }
    #endregion

    #region create or update user
    [HttpPost]
    public async Task<IActionResult> CreateUser(BusinessMainViewModel businessFormDetails)
    {
        try
        {
            BusinessMainViewModel businessMainVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessFormDetails.BusinessViewModelString);
            List<int> selectedRoles = JsonConvert.DeserializeObject<List<int>>(businessFormDetails.MappingRoles);
            if (businessMainVM.UserPermissionViewModel.Users == null)
            {
                businessMainVM.UserPermissionViewModel.Users = new();
            }
            ApplicationUser user = GetCurrentUserIdentity();
            if (user == null)
                return RedirectToAction("Index", "Business");


            int createdUserId;
            int createdPersonaldetailId;

            //update user
            if (businessFormDetails.UserPermissionViewModel.UserDetail.UserId != 0)
            {
                createdUserId = businessFormDetails.UserPermissionViewModel.UserDetail.UserId;
                if (businessFormDetails.UserPermissionViewModel.UserDetail.PersonalDetailId != null)
                {
                    createdPersonaldetailId = await _userService.UpdatePersonalDetails(businessFormDetails.UserPermissionViewModel.UserDetail, user.Id);
                }
                else
                {
                    createdPersonaldetailId = await _userService.SavePersonalDetails(businessFormDetails.UserPermissionViewModel.UserDetail, user.Id);
                }
                bool isMappingUpdated = await _userBusinessMappingService.UpdateUserBusinessMapping(createdUserId, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, selectedRoles, createdPersonaldetailId, user.Id);
                UserViewmodel updatedUser = businessMainVM.UserPermissionViewModel.Users.FirstOrDefault(x => x.UserId == createdUserId);
                for (int i = 0; i < selectedRoles.Count; i++)
                {
                    if (i == selectedRoles.Count - 1)
                    {
                        businessFormDetails.UserPermissionViewModel.UserDetail.RoleName += _roleService.GetRoleById(selectedRoles[i]).RoleName;
                    }
                    else
                    {
                        businessFormDetails.UserPermissionViewModel.UserDetail.RoleName += _roleService.GetRoleById(selectedRoles[i]).RoleName + ", ";
                    }
                }
                string loginUrl = Url.Action("Login", "Login", new { }, Request.Scheme);
                Task.Run(async () =>
                {
                    await CommonMethods.UpdateRoleEmail(businessFormDetails.UserPermissionViewModel.UserDetail.Email, businessFormDetails.UserPermissionViewModel.UserDetail.RoleName, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessName, loginUrl);
                });

                string message = string.Format(Messages.UserInBusinessActivity, businessFormDetails.UserPermissionViewModel.UserDetail.FirstName + " " + businessFormDetails.UserPermissionViewModel.UserDetail.LastName, "updated", user.FirstName + " " + user.LastName);
                await SetActivityLog(message, EnumHelper.Actiontype.Update, EnumHelper.ActivityEntityType.Business, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id, EnumHelper.ActivityEntityType.Role, businessFormDetails.UserPermissionViewModel.UserDetail.UserId);
            }
            else
            {
                //add user
                if (_userService.IsUserRegistered(businessFormDetails.UserPermissionViewModel.UserDetail.Email))
                {
                    businessFormDetails.UserPermissionViewModel.UserDetail.IsUserRegistered = true;
                }
                else
                {
                    businessFormDetails.UserPermissionViewModel.UserDetail.IsUserRegistered = false;
                }
                int personalDetailsId = await _userService.SavePersonalDetails(businessFormDetails.UserPermissionViewModel.UserDetail, user.Id);
                if (personalDetailsId == 0)
                {
                    return Json(false);
                }

                ApplicationUser userPresent = _userService.GetuserByEmail(businessFormDetails.UserPermissionViewModel.UserDetail.Email);

                if (userPresent == null)
                {
                    createdUserId = await _userService.SaveUser(businessFormDetails.UserPermissionViewModel.UserDetail);
                    if (createdUserId == 0)
                    {
                        return Json(false);
                    }
                }
                else
                {
                    businessFormDetails.UserPermissionViewModel.UserDetail.UserId = userPresent.Id;
                    businessFormDetails.UserPermissionViewModel.UserDetail.PersonalDetailId = personalDetailsId;
                    createdUserId = userPresent.Id;
                }
                for (int i = 0; i < selectedRoles.Count; i++)
                {
                    int userBusinessMappingId = await _userBusinessMappingService.SaveUserBusinessMapping(createdUserId, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, selectedRoles[i], personalDetailsId, user.Id);
                    if (userBusinessMappingId == 0)
                    {
                        return Json(false);
                    }
                    if (i == selectedRoles.Count - 1)
                    {
                        businessFormDetails.UserPermissionViewModel.UserDetail.RoleName += _roleService.GetRoleById(selectedRoles[i]).RoleName;
                    }
                    else
                    {
                        businessFormDetails.UserPermissionViewModel.UserDetail.RoleName += _roleService.GetRoleById(selectedRoles[i]).RoleName + ", ";
                    }
                }
                string loginLink = Url.Action("Login", "Login", new { }, Request.Scheme);
                if (userPresent == null)
                {
                    Task.Run(async () =>
                    {
                        CommonMethods.CreateUserAndRoleEmail(businessFormDetails.UserPermissionViewModel.UserDetail.Email, businessFormDetails.UserPermissionViewModel.UserDetail.RoleName, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessName, loginLink);
                    });
                }
                else
                {
                    Task.Run(async () =>
                    {
                        CommonMethods.CreateRoleEmail(businessFormDetails.UserPermissionViewModel.UserDetail.Email, businessFormDetails.UserPermissionViewModel.UserDetail.RoleName, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessName, loginLink);
                    });
                }
                string message = string.Format(Messages.AddUserInBusinessActivity, businessFormDetails.UserPermissionViewModel.UserDetail.FirstName + " " + businessFormDetails.UserPermissionViewModel.UserDetail.LastName, user.FirstName + " " + user.LastName);
                await SetActivityLog(message, EnumHelper.Actiontype.Add, EnumHelper.ActivityEntityType.Business, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id, EnumHelper.ActivityEntityType.Role, createdUserId);
                businessFormDetails.UserPermissionViewModel.UserDetail.UserId = createdUserId;
            }

            businessMainVM.UserPermissionViewModel.Users = await _userBusinessMappingService.GetUsersByBusiness(businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id);
            businessMainVM.UserPermissionViewModel.Users = _userBusinessMappingService.SetPermissions(businessMainVM.UserPermissionViewModel.Users, user.Id, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId);
            ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");

            return PartialView("_AddUserDetailsPartial", businessMainVM);
        }
        catch (Exception e)
        {
            return Json(false);
        }
    }
    #endregion

    #region Delete user from business
    [HttpPost]
    public async Task<IActionResult> DeleteUserFromBusiness(int userId, int roleId, int businessId, string businessVM)
    {
        BusinessMainViewModel businessMainVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessVM);

        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        UserViewmodel user1 = _userService.GetuserById(userId, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId);
        bool isDeletedMapping = await _userBusinessMappingService.DeleteUserBusinessMappingByBusinessId(userId, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id);
        UserViewmodel deletedUser = businessMainVM.UserPermissionViewModel.Users.FirstOrDefault(x => x.UserId == userId);
        businessMainVM.UserPermissionViewModel.Users.Remove(deletedUser);
        ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");
        string loginLink = Url.Action("Login", "Login", new { }, Request.Scheme);
        Task.Run(async () =>
                  {
                      CommonMethods.DeleteUserEmail(user1.Email, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessName, loginLink);
                  });
        // string message = string.Format(Messages.userToBusiness, businessFormDetails.UserPermissionViewModel.UserDetail.FirstName + " " + businessFormDetails.UserPermissionViewModel.UserDetail.LastName, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessName, businessFormDetails.UserPermissionViewModel.UserDetail.RoleName);
        string message = string.Format(Messages.UserInBusinessActivity, deletedUser.FirstName + " " + deletedUser.LastName, "deleted", user.FirstName + " " + user.LastName);
        // string message = string.Format(Messages.DeleteuserFromBusiness, user1.FirstName + " " + user1.LastName, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessName);

        await SetActivityLog(message, EnumHelper.Actiontype.Delete, EnumHelper.ActivityEntityType.Business, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id, EnumHelper.ActivityEntityType.Role, user1.UserId);
        return PartialView("_AddUserDetailsPartial", businessMainVM);
    }
    #endregion

    #region save business in database
    [HttpPost]
    public async Task<IActionResult> SaveBusiness(BusinessMainViewModel businessMainVM)
    {
        BusinessMainViewModel businessVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessMainVM.BusinessViewModelString);
        if (businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment != null)
        {
            if (businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinessLogo != null)
            {
                string[] extension = businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinessLogo.FileName.Split(".");
                string fileNameTemp = businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinessLogo.FileName;
                if (extension[extension.Length - 1] == "jpg" || extension[extension.Length - 1] == "jpeg" || extension[extension.Length - 1] == "png")
                {
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    string imageFileName = CommonMethods.UploadImage(businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinessLogo, path);

                    businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinesLogoPath = $"/uploads/{imageFileName}";
                    businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.FileExtension = extension[extension.Length - 1];
                    businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.FileName = fileNameTemp;
                    businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinessLogo = null;
                }
                else
                {
                    return Json(false);
                }
            }
        }
        else
        {
            if (businessVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment == null)
            {
                businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment = null;
                AttachmentViewModel attachment = new();
                attachment.BusinesLogoPath = null;
                attachment.FileExtension = null;
                attachment.FileName = null;
                attachment.BusinessLogo = null;
                businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment = attachment;
            }
            else
            {
                AttachmentViewModel attachment = new();
                attachment.BusinesLogoPath = businessVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinesLogoPath;
                attachment.FileExtension = businessVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.FileExtension!;
                attachment.FileName = businessVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.FileName!;
                attachment.BusinessLogo = null;
                businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment = attachment;
            }
        }
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");

        int businessId = await _businessService.SaveBusiness(businessMainVM.BusinessDetailViewModel.BusinessItem, user.Id);
        if (businessId == 0)
        {
            throw new Exception(Messages.GlobalAddUpdateFailMessage.Replace("{status}", "add").Replace("{name}", "business"));
            return Json(false);
        }

        businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId = businessId;
        businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessLogoAttachment.BusinessLogo = null;
        businessMainVM.BusinessDetailViewModel.BusinessCategories = _referenceDataEntityService.GetReferenceValues(EnumHelper.EntityType.BusinessCategory.ToString());
        businessMainVM.BusinessDetailViewModel.BusinessTypes = _referenceDataEntityService.GetReferenceValues(EnumHelper.EntityType.BusinessType.ToString());
        businessMainVM.BusinessDetailViewModel.Roles = _roleService.GetAllRoles();
        businessMainVM.BusinessViewModelString = null;
        ViewBag.Categories = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessCategories, "Id", "EntityValue");
        ViewBag.Types = new SelectList(businessMainVM.BusinessDetailViewModel.BusinessTypes, "Id", "EntityValue");
        ViewBag.Roles = new SelectList(businessMainVM.BusinessDetailViewModel.Roles, "RoleId", "RoleName");

        businessMainVM.UserPermissionViewModel.Users = await _userBusinessMappingService.GetUsersByBusiness(businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id);
        businessMainVM.UserPermissionViewModel.Users = _userBusinessMappingService.SetPermissions(businessMainVM.UserPermissionViewModel.Users, user.Id, businessId);

        return PartialView("_AddBusinessDetailsPartial", businessMainVM);
    }
    #endregion

    #region get users by business
    public async Task<IActionResult> GetUsersByBusiness(string businessVM)
    {
        BusinessMainViewModel businessMainVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessVM);
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        if (businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId != 0)
        {
            businessMainVM.UserPermissionViewModel.Users = await _userBusinessMappingService.GetUsersByBusiness(businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id);
            businessMainVM.UserPermissionViewModel.Users = _userBusinessMappingService.SetPermissions(businessMainVM.UserPermissionViewModel.Users, user.Id, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId);

            return PartialView("_AddUserDetailsPartial", businessMainVM);
        }
        businessMainVM.BusinessViewModelString = null;
        return PartialView("_AddBusinessDetailsPartial", businessMainVM);
    }
    #endregion

    #region check if user is same as loginuser
    public IActionResult IsUserSameAsLoginUser(BusinessMainViewModel businessMainVM)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        if (user.Email.ToLower().Trim() == businessMainVM.UserPermissionViewModel.UserDetail.Email.ToLower().Trim())
        {
            return Json(true);
        }
        return Json(false);
    }
    #endregion

    #region check if user added is main owner
    public IActionResult IsUserMainOwner(BusinessMainViewModel businessMainVM)
    {
        BusinessMainViewModel businessMainVMdeserialized = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessMainVM.BusinessViewModelString);
        ApplicationUser user = _userService.GetuserByEmail(businessMainVM.UserPermissionViewModel.UserDetail.Email);
        if (user != null)
        {
            bool isMainOwner = _userBusinessMappingService.IsMainOwner(businessMainVMdeserialized.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id);
            if (isMainOwner)
            {
                return Json(true);
            }
        }
        return Json(false);
    }

    #endregion

    #region delete business
    public async Task<IActionResult> DeleteBusiness(int businessId)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        bool isBusinessdeleted = await _businessService.DeleteBusiness(businessId, user.Id);
        if (isBusinessdeleted)
        {
            return Json(new { success = true, message = Messages.GlobalAddUpdateMesage.Replace("{name}", "Business").Replace("{status}", "deleted") });
        }
        else
        {
            return Json(new { success = false, message = Messages.GlobalAddUpdateMesage.Replace("{name}", "business").Replace("{status}", "delete") });
        }
    }
    #endregion

    #region  SetBusinessCookie
    public IActionResult SetBusinessCookie(int businessId)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        string businessToken = _jwttokenService.GenerateBusinessToken(businessId);
        _cookieService.SetCookie(Response, TokenKey.BusinessToken, businessToken);
        _cookieService.SetCookie(Response, TokenKey.BusinessId, businessId.ToString());
        List<BusinessViewModel> businessList = _businessService.GetBusinesses(user.Id);
        List<string> businessNamesList = new();
        foreach (BusinessViewModel business in businessList)
        {
            string businessName = business.BusienssName + "-" + business.BusinessId;
            businessNamesList.Add(businessName);
        }
        string businessNamesMerged = string.Join(",", businessNamesList);
        _cookieService.SetCookie(Response, TokenKey.AllBusinesses, businessNamesMerged);

        return Json(new { success = true });
    }
    #endregion

    #region setBusinessToken
    public IActionResult SetBusinessToken(int businessId)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        string businessToken = _jwttokenService.GenerateBusinessToken(businessId);
        _cookieService.SetCookie(Response, TokenKey.BusinessToken, businessToken);
        _cookieService.SetCookie(Response, TokenKey.BusinessId, businessId.ToString());
        return Json(new { success = true });
    }
    #endregion

    #region inactive / active use
    public async Task<IActionResult> ActiveInactiveUser(int userId, bool isActive, int businessId, string businessVM)
    {
        ViewResponseModel partialViewResponseModel = new();

        BusinessMainViewModel businessMainVM = JsonConvert.DeserializeObject<BusinessMainViewModel>(businessVM);
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
        {
            partialViewResponseModel.RedirectUrl = Url.Action("Index", "Business");
            return Json(partialViewResponseModel);
        }

        // return RedirectToAction("Index", "Business");
        bool isUserUpdated = await _userBusinessMappingService.ActiveInactiveUser(userId, businessId, isActive, user.Id);
        businessMainVM.UserPermissionViewModel.Users = await _userBusinessMappingService.GetUsersByBusiness(businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId, user.Id);
        businessMainVM.UserPermissionViewModel.Users = _userBusinessMappingService.SetPermissions(businessMainVM.UserPermissionViewModel.Users, user.Id, businessMainVM.BusinessDetailViewModel.BusinessItem.BusinessId);
        if (isUserUpdated)
        {
            string message;
            UserViewmodel userUpdated = _userService.GetuserById(userId, businessId);
            if (isActive)
            {
                message = string.Format(Messages.UserInBusinessActivity, userUpdated.FirstName + " " + userUpdated.LastName, "activated", user.FirstName + " " + user.LastName);
                await SetActivityLog(message, EnumHelper.Actiontype.Update, EnumHelper.ActivityEntityType.Business, businessId, user.Id, EnumHelper.ActivityEntityType.Role, userId);
                partialViewResponseModel = await PartialViewResponse("Business/_AddUserDetailsPartial.cshtml", businessMainVM, Messages.GlobalAddUpdateMesage.Replace("{name}", "User").Replace("{status}", "activated"));
            }
            else
            {
                message = string.Format(Messages.UserInBusinessActivity, userUpdated.FirstName + " " + userUpdated.LastName, "inactivated", user.FirstName + " " + user.LastName);
                await SetActivityLog(message, EnumHelper.Actiontype.Update, EnumHelper.ActivityEntityType.Business, businessId, user.Id, EnumHelper.ActivityEntityType.Role, userId);
                partialViewResponseModel = await PartialViewResponse("Business/_AddUserDetailsPartial.cshtml", businessMainVM, Messages.GlobalAddUpdateMesage.Replace("{name}", "User").Replace("{status}", "inactivated"));
            }
        }
        else
        {
            if (isActive)
            {
                partialViewResponseModel = await PartialViewResponse("Business/_AddUserDetailsPartial.cshtml", businessMainVM, Messages.GlobalAddUpdateFailMessage.Replace("{name}", "user").Replace("{status}", "activate"), ErrorType.Error);
            }
            else
            {
                partialViewResponseModel = await PartialViewResponse("Business/_AddUserDetailsPartial.cshtml", businessMainVM, Messages.GlobalAddUpdateFailMessage.Replace("{name}", "user").Replace("{status}", "inactivate"), ErrorType.Error);
            }
        }
        return Json(partialViewResponseModel);

    }
    #endregion


}