using System.Reflection.Metadata;
using BusinessAcessLayer.Interface;
using DataAccessLayer.ViewModels;
using Microsoft.AspNetCore.Mvc;
using BusinessAcessLayer.Constant;
using BusinessAcessLayer.Helper;
using System.Threading.Tasks;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using BusinessAcessLayer.Services;
using Microsoft.AspNetCore.Identity;

namespace LedgerBook.Controllers;

public class LoginController : BaseController
{
    private readonly IUserService _userService;
    private readonly IJWTTokenService _jwtTokenService;
    private readonly ICookieService _cookieService;
    private readonly IAttachmentService _attachmentService;
    public LoginController(
        ILoginService loginService,
        IUserService userService,
        IJWTTokenService jWTTokenService,
        IViewRenderService viewRenderService,
        ICookieService cookieService,
        IAttachmentService attachmentService,
        IActivityLogService activityLogService)
            : base(loginService, viewRenderService, activityLogService)
    {
        _userService = userService;
        _jwtTokenService = jWTTokenService;
        _cookieService = cookieService;
        _attachmentService = attachmentService;
    }

    #region login get method
    [HttpGet]
    public async Task<IActionResult> Login()
    {
        ViewData["loginButton"] = "Login";
        string token = Request.Cookies[TokenKey.UserToken];
        if (token != null)
        {
            ApplicationUser user = _loginService.GetUserFromTokenIdentity(token);
            if (user == null)
            {
                return View();
            }
            else
            {
                if (user.ProfileAttachmentId != null)
                {
                    AttachmentViewModel attachmentViewModel = _attachmentService.GetAttachmentById((int)user.ProfileAttachmentId);
                    _cookieService.SetCookie(Response, TokenKey.ProfilePhoto, attachmentViewModel.BusinesLogoPath);
                }
                _cookieService.SetCookie(Response, TokenKey.UserName, user.FirstName + " " + user.LastName);
                return RedirectToAction("Index", "Business");
            }
        }
        return View();
    }
    #endregion

    #region render login sign up partial view
    public IActionResult RenderLoginform(string formType)
    {
        if (formType == "Signup")
        {
            RegistrationViewModel registrationViewModel = new();
            return PartialView("_RegistrationFormPartial", registrationViewModel);
        }
        else
        {
            LoginViewModel loginViewModel = new();
            return PartialView("_LoginFormPartial", loginViewModel);
        }
    }
    #endregion

    #region render reset password partial view
    public IActionResult RenderResetpassword(string email)
    {
        ResetPasswordViewModel resetPasswordViewModel = new();
        resetPasswordViewModel.Email = email;
        return PartialView("_RegistrationFormPartial", resetPasswordViewModel);
    }
    #endregion

    #region registration post method
    [HttpPost]
    public async Task<IActionResult> Registration(RegistrationViewModel registrationViewModel)
    {
        ViewResponseModel partialViewResponseModel = new();
        if (registrationViewModel.Email == null || registrationViewModel.Password == null)
        {
            partialViewResponseModel = await PartialViewResponse("Login/_RegistrationFormPartial.cshtml", registrationViewModel, Messages.InvalidCredentilMessage, ErrorType.Error);
        }
        else
        {
            if (_loginService.IsEmailExist(registrationViewModel.Email) &&
            _userService.IsUserRegistered(registrationViewModel.Email))
            {
                partialViewResponseModel = await PartialViewResponse("Login/_RegistrationFormPartial.cshtml", registrationViewModel, Messages.EmailExistMessage, ErrorType.Error);
            }
            else
            {
                if (await _loginService.SaveUser(registrationViewModel))
                {
                    string verificationToken = _loginService.GetEmailVerifiactionToken(registrationViewModel.Email);
                    string verificationLink = Url.Action("VerifyEmail", "Login", new { verificationCode = _jwtTokenService.GenerateTokenEmailVerificationToken(registrationViewModel.Email, verificationToken) }, Request.Scheme);
                    string loginLink = Url.Action("Login", "Login", new { }, Request.Scheme);
                    _ = CommonMethods.RegisterEmail(registrationViewModel.FirstName + " " + registrationViewModel.LastName, registrationViewModel.Email, verificationLink, loginLink);
                    LoginViewModel loginViewModel = new();
                    partialViewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.RegistrationSuccessMessage, ErrorType.Success);
                }
                else
                {
                    partialViewResponseModel = await PartialViewResponse("Login/_RegistrationFormPartial.cshtml", registrationViewModel, Messages.ExceptionMessage, ErrorType.Error);
                }
            }
        }
        return Json(partialViewResponseModel);
    }
    #endregion

    #region verify email
    public async Task<IActionResult> VerifyEmail(string verificationCode)
    {
        string email = _jwtTokenService.GetClaimValue(verificationCode, "email");
        string emailToken = _jwtTokenService.GetClaimValue(verificationCode, "token");
        bool isEmailVerified = await _loginService.EmailVerification(email, emailToken);
        if (isEmailVerified)
        {
            TempData["SuccessMessage"] = Messages.VerificationSuccessMessage;
            return RedirectToAction("Login", "Login");
        }
        else
        {
            TempData["ErrorMessage"] = Messages.VerificationErrorMessage;
            return RedirectToAction("Login", "Login");
        }
    }
    #endregion

    #region login post method
    [HttpPost]
    public async Task<IActionResult> LoginAsync(LoginViewModel loginViewModel)
    {
        ViewResponseModel viewResponseModel = new();
        if (loginViewModel.Email == null || loginViewModel.Password == null)
        {
            viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.InvalidCredentilMessage, ErrorType.Error);
        }
        else
        {
            if (!_loginService.IsEmailExist(loginViewModel.Email))
            {
                viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.EmailDoesNotExistMessage, ErrorType.Error);
            }
            else if (!_userService.IsUserRegistered(loginViewModel.Email))
            {
                viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.EmailDoesNotExistMessage, ErrorType.Error);
            }
            else
            {
                if (!_loginService.IsEmailVerified(loginViewModel.Email))
                {
                    viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.NotVerifiedEmailMessae, ErrorType.Error);
                }
                else
                {
                    string verificaitonToken = await _loginService.VerifyPassword(loginViewModel);
                    if (verificaitonToken != null)
                    {
                        if (verificaitonToken != null)
                        {
                            _cookieService.SetCookie(Response, TokenKey.UserToken, verificaitonToken);
                            ApplicationUser user = _loginService.GetUserFromTokenIdentity(verificaitonToken);
                            if (user == null)
                            {
                                return View();
                            }
                            else
                            {
                                if (user.ProfileAttachmentId != null)
                                {
                                    AttachmentViewModel attachmentViewModel = _attachmentService.GetAttachmentById((int)user.ProfileAttachmentId);
                                    _cookieService.SetCookie(Response, TokenKey.ProfilePhoto, attachmentViewModel.BusinesLogoPath);
                                }
                                _cookieService.SetCookie(Response, TokenKey.UserName, user.FirstName + " " + user.LastName);

                            }

                        }

                        if (loginViewModel.RememberMe)
                        {
                            _cookieService.SetCookie(Response, TokenKey.RememberMe, loginViewModel.Email);
                        }
                        viewResponseModel.RedirectUrl = Url.Action("Index", "Business");
                    }
                    else
                    {
                        viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.InvalidCredentilMessage, ErrorType.Error);
                    }
                }
            }

        }
        return Json(viewResponseModel);
    }
    #endregion

    #region logout
    public IActionResult Logout()
    {
        _cookieService.DeleteAllCookies(Request, Response);
        return RedirectToAction("Login", "Login");
    }
    #endregion

    #region forgot password get
    public async Task<IActionResult> ForgotPassword(string? email)
    {
        ViewResponseModel viewResponseModel = new();
        LoginViewModel loginViewModel = new();

        if (email != null)
        {
            if (_loginService.IsEmailExist(email))
            {
                if (_userService.IsUserRegistered(email))
                {
                    loginViewModel.Email = email;
                    viewResponseModel = await PartialViewResponse("Login/_ForgotPasswordPartial.cshtml", loginViewModel);
                }
                else
                {
                    viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.EmailDoesNotExistMessage, ErrorType.Error);
                }
            }
            else
            {
                viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.EmailDoesNotExistMessage, ErrorType.Error);
            }

        }
        else
        {
            viewResponseModel = await PartialViewResponse("Login/_ForgotPasswordPartial.cshtml", loginViewModel);
        }
        return Json(viewResponseModel);
    }
    #endregion

    #region forgot password post
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(LoginViewModel loginViewModel)
    {
        ViewResponseModel viewResponseModel = new();

        if (loginViewModel.Email != null)
        {
            if (_loginService.IsEmailExist(loginViewModel.Email))
            {
                if (_userService.IsUserRegistered(loginViewModel.Email))
                {
                    //send email
                    ApplicationUser user = _userService.GetuserByEmail(loginViewModel.Email);
                    string username = user.FirstName + " " + user.LastName;
                    string resetLink = Url.Action("ResetPassword", "Login", new { resetPasswordToken = _jwtTokenService.GenerateTokenEmailPassword(loginViewModel.Email, user.PasswordHash) }, Request.Scheme);
                    string loginLink = Url.Action("Login", "Login", new { }, Request.Scheme);
                    _ = CommonMethods.ResetPasswordEmail(loginViewModel.Email, username, resetLink, loginLink);
                    viewResponseModel = await PartialViewResponse("Login/_LoginFormPartial.cshtml", loginViewModel, Messages.SendResetPasswordMailSuccess);
                }
                else
                {
                    viewResponseModel = await PartialViewResponse("Login/_ForgotPasswordPartial.cshtml", loginViewModel, Messages.EmailDoesNotExistMessage, ErrorType.Error);
                }
            }
            else
            {
                viewResponseModel = await PartialViewResponse("Login/_ForgotPasswordPartial.cshtml", loginViewModel, Messages.EmailDoesNotExistMessage, ErrorType.Error);
            }

        }
        else
        {
            //error
        }
        return Json(viewResponseModel);
    }
    #endregion

    #region reset password
    public IActionResult ResetPassword(string resetPasswordToken)
    {
        try
        {
            string email = _jwtTokenService.GetClaimValue(resetPasswordToken, "email");
            string newpassword = _jwtTokenService.GetClaimValue(resetPasswordToken, "password");
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(newpassword) || !_loginService.IsEmailExist(email))
            {
                TempData["ErrorMessage"] = Messages.InvalidResetPasswordLink;
                return RedirectToAction("Login", "Login");
            }
            ApplicationUser user = _userService.GetuserByEmail(email);
            string savedPassword = user.PasswordHash;

            if (savedPassword == newpassword)
            {
                ResetPasswordViewModel resetPasswordViewModel = new();
                resetPasswordViewModel.Email = email;
                return View(resetPasswordViewModel);
            }
            TempData["ErrorMessage"] = Messages.LinkAlreadyUsedMessage;
            return RedirectToAction("Login", "Login");
        }
        catch (Exception e)
        {
            TempData["ErrorMessage"] = Messages.InvalidResetPasswordLink;
            return RedirectToAction("Login", "Login");
        }

    }
    #endregion

    #region reset password post
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel resetPasswordViewModel)
    {
        ApplicationUser user = _userService.GetuserByEmail(resetPasswordViewModel.Email);
        if (user != null)
        {
            PasswordHasher<ApplicationUser> passwordHasher = new PasswordHasher<ApplicationUser>();
            PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, resetPasswordViewModel.Password);
            if (result != PasswordVerificationResult.Failed)
            {
                TempData["ErrorMessage"] = Messages.SamePasswordsErrorMessage;
                return View();
            }
            else
            {
                bool IsPasswordUpdated = await _userService.UpdatePassword(resetPasswordViewModel);
                if (IsPasswordUpdated)
                {
                    TempData["SuccessMessage"] = string.Format(Messages.GlobalAddUpdateMesage, "Password", "updated");
                    return RedirectToAction("Login", "Login");
                }
                else
                {
                    TempData["ErrorMessage"] = string.Format(Messages.GlobalAddUpdateFailMessage, "update", "Password");
                    return View();
                }
            }
        }
        else
        {
            TempData["ErrorMessage"] = Messages.UserNotExistMessage;
            return View();
        }
    }
    #endregion
}