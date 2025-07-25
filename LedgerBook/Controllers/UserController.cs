using System.Threading.Tasks;
using BusinessAcessLayer.Constant;
using BusinessAcessLayer.Helper;
using BusinessAcessLayer.Interface;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LedgerBook.Controllers;

public class UserController : BaseController
{
    private readonly IUserService _userService;
    private readonly ICookieService _cookieService;
    private readonly IAttachmentService _attachmentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public UserController(ILoginService loginService,
    IUserService userService,
    IAttachmentService attachmentService,
    IViewRenderService viewRenderService,
    ICookieService cookieService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IActivityLogService activityLogService)
     : base(loginService, viewRenderService, activityLogService)
    {
        _userService = userService;
        _cookieService = cookieService;
        _attachmentService = attachmentService;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    #region profile index page
    public IActionResult Profile()
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Login", "Login");
        UserProfileViewModel userProfileViewModel = _userService.GetUserProfile(user.Id);
        return View(userProfileViewModel);
    }
    #endregion

    #region profile post method
    [HttpPost]
    public async Task<IActionResult> Profile(UserProfileViewModel userProfileViewModel)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Login", "Login");
        if (userProfileViewModel.AttachmentViewModel != null)
        {
            if (userProfileViewModel.AttachmentViewModel.BusinessLogo != null)
            {
                string[] extension = userProfileViewModel.AttachmentViewModel.BusinessLogo.FileName.Split(".");
                string fileNameTemp = userProfileViewModel.AttachmentViewModel.BusinessLogo.FileName;
                if (extension[extension.Length - 1] == "jpg" || extension[extension.Length - 1] == "jpeg" || extension[extension.Length - 1] == "png")
                {
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    string imageFileName = CommonMethods.UploadImage(userProfileViewModel.AttachmentViewModel.BusinessLogo, path);

                    userProfileViewModel.AttachmentViewModel.BusinesLogoPath = $"/uploads/{imageFileName}";
                    userProfileViewModel.AttachmentViewModel.FileExtension = extension[extension.Length - 1];
                    userProfileViewModel.AttachmentViewModel.FileName = fileNameTemp;
                }
                else
                {
                    TempData["ErrorMessage"] = Messages.InvalidImageExtensionMessage;
                    return RedirectToAction("Profile");
                }
            }
        }
        else
        {
            userProfileViewModel.AttachmentViewModel = new();
        }
        bool isUserUpdated = await _userService.UpdateUserProfile(userProfileViewModel);
        ApplicationUser updatedUser = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Login", "Login");
        if (isUserUpdated)
        {
            if (updatedUser.ProfileAttachmentId != null)
            {
                AttachmentViewModel attachmentViewModel = _attachmentService.GetAttachmentById((int)updatedUser.ProfileAttachmentId);
                _cookieService.SetCookie(Response, TokenKey.ProfilePhoto, attachmentViewModel.BusinesLogoPath);
                _cookieService.SetCookie(Response, TokenKey.UserName, updatedUser.FirstName + " " + updatedUser.LastName);
            }
            TempData["SuccessMessage"] = string.Format(Messages.GlobalAddUpdateMesage, "User Profile", "Updated");
            string partyType = _cookieService.GetCookie(Request, TokenKey.PartyType);
            if (partyType != null)
            {
                return RedirectToAction("ManageBusiness", "Party");
            }
            else
            {
                return RedirectToAction("Index", "Business");
            }
        }
        else
        {
            TempData["ErrorMessage"] = string.Format(Messages.GlobalAddUpdateFailMessage, "update", "user Profile");
            return RedirectToAction("Profile");

        }
    }
    #endregion

    #region render change password modal
    public async Task<IActionResult> RenderChangePassword()
    {
        ViewResponseModel viewResponseModel = new();
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Login", "Login");
        ChangePasswordViewModel changePasswordViewModel = new();
        changePasswordViewModel.Email = user.Email;
        return PartialView("_ChangePasswordModalpartial", changePasswordViewModel);
    }
    #endregion

    #region chage password post
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel changePasswordViewModel)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Login", "Login");

        if (user != null)
        {
            if (!await _loginService.IsCorrectOldpassword(user.Email, changePasswordViewModel.OldPassword))
            {
                return Json(new { success = false, message = Messages.IncorrectOldPAssword });
            }
            else
            {
                ResetPasswordViewModel resetPasswordViewModel = new();
                resetPasswordViewModel.Email = user.Email;
                resetPasswordViewModel.Password = changePasswordViewModel.Password;
                bool ispasswordUpdated = await _userService.UpdatePassword(resetPasswordViewModel);
                if (ispasswordUpdated)
                {
                    string loginLink = Url.Action("Login", "Login", new { }, Request.Scheme);
                    _ = CommonMethods.ChangePasswordEmail(user.Email, user.FirstName + " " + user.LastName, loginLink);
                    return Json(new { success = true, message = string.Format(Messages.GlobalAddUpdateMesage, "Password", "updated") });
                }
                else
                {
                    return Json(new { success = false, message = string.Format(Messages.GlobalAddUpdateFailMessage, "update", "password") });
                }
            }
        }
        else
        {
            return Json(new { success = false, message = Messages.ExceptionMessage });
        }

    }
    #endregion
}
