using System.Threading.Tasks;
using BusinessAcessLayer.Interface;
using BusinessAcessLayer.Constant;
using DataAccessLayer.Constant;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using LedgerBook.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessAcessLayer.Helper;

namespace LedgerBook.Controllers;

public class PartyController : BaseController
{
    private readonly IBusinessService _businessService;

    private readonly IUserBusinessMappingService _userBusinessMappingService;
    private readonly IPartyService _partyService;
    private readonly IReferenceDataEntityService _referenceDataEntityService;
    private readonly ICookieService _cookieService;
    private readonly IJWTTokenService _jwtTokenService;
    public PartyController(ILoginService loginService,
    IViewRenderService viewRenderService,
    IUserBusinessMappingService userBusinessMappingService,
    IBusinessService businessService,
    IPartyService partyService,
    IReferenceDataEntityService referenceDataEntityService,
    ICookieService cookieService,
    IJWTTokenService jWTTokenService,
    IActivityLogService activityLogService
    ) : base(loginService, viewRenderService, activityLogService)
    {
        _businessService = businessService;
        _partyService = partyService;
        _userBusinessMappingService = userBusinessMappingService;
        _referenceDataEntityService = referenceDataEntityService;
        _cookieService = cookieService;
        _jwtTokenService = jWTTokenService;

    }

    #region magnage business index page
    public IActionResult ManageBusiness()
    {
        // IActionResult redirectResult = RedirectToIndexIfBusinessNotFound();
        // if (redirectResult != null)
        // {
        //     return redirectResult;
        // }
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        Businesses business = GetBusinessFromToken();
        if (business == null)
        {
            return RedirectToAction("Index", "Business");
        }
        string partyType2 = _cookieService.GetCookie(Request, TokenKey.PartyType);
        if (partyType2 == PartyType.Customer || partyType2 == PartyType.Supplier)
        {
            PartyTransactionViewModel partyTransactionVM = new();
            partyTransactionVM.BusinessId = business.Id;
            partyTransactionVM.BusinessName = business.BusinessName;
            partyTransactionVM.Parties = _partyService.GetPartiesByType(partyType2, business.Id, "", "-1", "-1");
            partyTransactionVM.PartyTypeString = partyType2;

            ViewData["sidebar"] = partyType2;

            return View(partyTransactionVM);
        }
        else
        {
            return RedirectToAction("PageNotFoundError", "ErrorPage");
        }
    }
    #endregion

    #region check roles and permission
    public IActionResult CheckRolePermission()
    {
        // IActionResult redirectResult = RedirectToIndexIfBusinessNotFound();
        // if (redirectResult != null)
        // {
        //     return redirectResult;
        // }
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        Businesses business = GetBusinessFromToken();

        if (business == null)
        {
            return RedirectToAction("Index", "Business");
        }
        List<RoleViewModel> rolesByUser = _userBusinessMappingService.GetRolesByBusinessId(business.Id, user.Id);
        _cookieService.SetCookie(Response, TokenKey.BusinessName, business.BusinessName);

        if (rolesByUser.Any(role => role.RoleName == "Owner/Admin" || role.RoleName == "Sales Manager"))
        {
            return RedirectToAction("DisplayCustomers");
        }
        else if (rolesByUser.Any(role => role.RoleName == "Purchase Manager"))
        {
            return RedirectToAction("DisplaySuppliers");
        }
        else
        {
            return RedirectToAction("PageNotFoundError", "ErrorPage");
        }

    }
    #endregion

    #region display customers
    [PermissionAuthorize("SalesManager")]
    public IActionResult DisplayCustomers()
    {
        ViewData["IsSalesManager"] = false;
        _cookieService.SetCookie(Response, TokenKey.PartyType, PartyType.Customer);
        return RedirectToAction("ManageBusiness");
    }
    #endregion

    #region diaplay suppliers
    [PermissionAuthorize("PurchaseManager")]
    public IActionResult DisplaySuppliers()
    {
        ViewData["IsPurchaseManager"] = true;
        _cookieService.SetCookie(Response, TokenKey.PartyType, PartyType.Supplier);
        return RedirectToAction("ManageBusiness");
    }
    #endregion

    #region get all parties with search and filters
    public IActionResult GetAllParties(string partyType, string searchText = "", string filter = "-1", string sort = "-1")
    {
        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Buisness");
        List<PartyViewModel> Parties = _partyService.GetPartiesByType(partyType, business.Id, searchText, filter, sort);
        return PartialView("_DisplayPartiesPartial", Parties);
    }
    #endregion

    #region render save party modal
    public IActionResult RenderPartyModal(int businessId)
    {
        PartyTransactionViewModel partyTransactionVM = new();
        partyTransactionVM.BusinessId = businessId;
        partyTransactionVM.PartyViewModel = new();
        partyTransactionVM.PartyViewModel.BusinessId = businessId;
        return PartialView("_savePartyModalPartial", partyTransactionVM);
    }
    #endregion

    #region update party modal
    public IActionResult RenderUpdatePartyModal(int partyId)
    {
        PartyTransactionViewModel partyTransactionVM = new();
        partyTransactionVM.PartyViewModel = _partyService.GetPartyById(partyId);
        partyTransactionVM.PartyTypeString = _referenceDataEntityService.GetReferenceValueById(partyTransactionVM.PartyViewModel.PartyTypId);
        return PartialView("_savePartyModalPartial", partyTransactionVM);
    }
    #endregion

    #region save party post method
    public async Task<IActionResult> SaveParty(PartyTransactionViewModel partyTransactionVM)
    {
        ViewResponseModel partialViewResponseModel = new();
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
        {
            partialViewResponseModel.RedirectUrl = Url.Action("Login", "Login");
            return Json(partialViewResponseModel);
        }
        int userId = user.Id;
        Businesses business = GetBusinessFromToken();
        if (business == null)
        {
            partialViewResponseModel.RedirectUrl = Url.Action("Index", "Business");
            return Json(partialViewResponseModel);
        }
        //check if email changes or not
        if (partyTransactionVM.PartyViewModel.PartyId != 0)
        {
            if (_partyService.IsEmailChanged(partyTransactionVM.PartyViewModel))
            {
                partyTransactionVM.PartyViewModel.IsEmailChaneged = true;
            }
            else
            {
                partyTransactionVM.PartyViewModel.IsEmailChaneged = false;
            }
        }

        int partyId = await _partyService.SavePartyDetails(partyTransactionVM.PartyViewModel, userId, partyTransactionVM.PartyTypeString);
        if (partyTransactionVM.PartyViewModel.PartyId == 0 || partyTransactionVM.PartyViewModel.IsEmailChaneged)
        {
            string verificationToken = _partyService.GetEmailVerifiactionTokenForParty(partyId);
            string verificationLink = Url.Action("VerifyPartyEmail", "Party", new { verificationCode = _jwtTokenService.GenerateTokenPartyEmailVerification(partyTransactionVM.PartyViewModel.Email, verificationToken, partyId, business.BusinessName, partyTransactionVM.PartyTypeString) }, Request.Scheme);
            _ = CommonMethods.VerifyParty(partyTransactionVM.PartyViewModel.PartyName, partyTransactionVM.PartyViewModel.Email, verificationLink, partyTransactionVM.PartyTypeString, business.BusinessName);
        }

        partyTransactionVM.PartyViewModel.PartyId = partyId;
        partyTransactionVM.PartyViewModel.PartyTypeString = partyTransactionVM.PartyTypeString;
        if (partyTransactionVM.PartyViewModel.Amount != null)
        {
            TransactionEntryViewModel transactionViewModel = new();
            transactionViewModel.TransactionAmount = (Decimal)partyTransactionVM.PartyViewModel.Amount;
            transactionViewModel.TransactionType = (byte)partyTransactionVM.PartyViewModel.TransactionType;
            transactionViewModel.PartyId = partyId;
            transactionViewModel.BusinessName = business.BusinessName;
            int transactionId = await _partyService.SaveTransactionEntry(transactionViewModel, userId);
            if (transactionId != 0)
            {
                return Json(await PartialViewResponse("Party/_savePartyModalPartial.cshtml", partyTransactionVM, Messages.PartyAddedWithOpeningBalance));
            }
            else
            {
                return Json(await PartialViewResponse("Party/_savePartyModalPartial.cshtml", partyTransactionVM, string.Format(Messages.GlobalAddUpdateMesage, "Party", "added")));
            }
        }
        if (partyTransactionVM.PartyViewModel.PartyId != 0)
        {
            if (partyId != 0)
            {
                return Json(await PartialViewResponse("Party/_savePartyModalPartial.cshtml", partyTransactionVM, string.Format(Messages.GlobalAddUpdateMesage, "Party", "updated")));
            }
            else
            {
                return Json(await PartialViewResponse("Party/_savePartyModalPartial.cshtml", partyTransactionVM, string.Format(Messages.GlobalAddUpdateFailMessage, "update", "party"), ErrorType.Error));
            }
        }
        else
        {
            if (partyId != 0)
            {
                return Json(await PartialViewResponse("Party/_savePartyModalPartial.cshtml", partyTransactionVM, string.Format(Messages.GlobalAddUpdateMesage, "Party", "added")));
            }
            else
            {
                return Json(await PartialViewResponse("Party/_savePartyModalPartial.cshtml", partyTransactionVM, string.Format(Messages.GlobalAddUpdateFailMessage, "add", "party"), ErrorType.Error));
            }
        }
    }
    #endregion

    #region verify party email
    public async Task<IActionResult> VerifyPartyEmail(string verificationCode)
    {
        PartyVerifiedViewModel partyVerifiedVM = new();
        partyVerifiedVM.Email = _jwtTokenService.GetClaimValue(verificationCode, "email");
        partyVerifiedVM.Token = _jwtTokenService.GetClaimValue(verificationCode, "token");
        partyVerifiedVM.PartyId = int.Parse(_jwtTokenService.GetClaimValue(verificationCode, "partyId"));
        partyVerifiedVM.BusinessName = _jwtTokenService.GetClaimValue(verificationCode, "businessName");
        partyVerifiedVM.PartyType = _jwtTokenService.GetClaimValue(verificationCode, "partyType");

        bool isEmailVerified = await _partyService.PartyEmailVerification(partyVerifiedVM);
        if (isEmailVerified)
        {
            partyVerifiedVM.IsEmailVerified = true;
            // TempData["SuccessMessage"] = Messages.VerificationSuccessMessage;
            return RedirectToAction("EmailVerifiedForParty", "Party", partyVerifiedVM);
        }
        else
        {
            // TempData["ErrorMessage"] = Messages.VerificationErrorMessage;
            return RedirectToAction("EmailVerifiedForParty", "Party", new { partyVerifiedVM });
        }
    }
    #endregion

    #region email verification 
    public IActionResult EmailVerifiedForParty(PartyVerifiedViewModel partyVerifiedViewModel)
    {
        return View(partyVerifiedViewModel);
    }
    #endregion

    #region display paty and transaction details
    public IActionResult DisplayPartyTransactionDetails(int partyId)
    {
        PartyTransactionViewModel partyTransactionVM = new();
        partyTransactionVM.PartyViewModel = _partyService.GetPartyById(partyId);

        return PartialView("_PartyTransactionDetailsPartial", partyTransactionVM);
    }
    #endregion

    #region display transaction entries
    public IActionResult DisplyTransationEntries(int partyId)
    {
        LedgerEntriesViewModel ledgerEntriesVM = new();
        ledgerEntriesVM.TransactionsList = _partyService.GetTransactionsByPartyId(partyId);
        ledgerEntriesVM.NetBalance = 0;
        foreach (TransactionEntryViewModel entry in ledgerEntriesVM.TransactionsList)
        {
            if (entry.TransactionType == (byte)EnumHelper.TransactionType.GAVE)
            {
                ledgerEntriesVM.NetBalance += entry.TransactionAmount;
            }
            else
            {
                ledgerEntriesVM.NetBalance -= entry.TransactionAmount;
            }
        }
        ledgerEntriesVM.IspartyVerified = _partyService.IsPartyverified(partyId);
        ledgerEntriesVM.NetBalance = ledgerEntriesVM.TransactionsList.Where(x => x.TransactionType == (byte)EnumHelper.TransactionType.GAVE).Sum(a => a.TransactionAmount) - ledgerEntriesVM.TransactionsList.Where(x => x.TransactionType == (byte)EnumHelper.TransactionType.GOT).Sum(a => a.TransactionAmount);
        return PartialView("_TranactionEntriesPartial", ledgerEntriesVM);
    }
    #endregion

    #region save transaction modal display
    public IActionResult RenderSaveTransactionEntryModal(int? partyId, EnumHelper.TransactionType? transactionType, int? transactionId)
    {
        //display modal to  add entry
        if (transactionId == null)
        {
            TransactionEntryViewModel transactionEntryVM = new();
            transactionEntryVM.PartyId = (int)partyId;
            transactionEntryVM.TransactionTypeEnum = (EnumHelper.TransactionType)transactionType;
            return PartialView("_SaveTransactionEntryModal", transactionEntryVM);
        }
        else
        {
            //display edit entry modal
            TransactionEntryViewModel transactionEntryVM = _partyService.GetTransactionbyTransactionId((int)transactionId);
            transactionEntryVM.TransactionTypeEnum = (EnumHelper.TransactionType)transactionEntryVM.TransactionType;
            return PartialView("_SaveTransactionEntryModal", transactionEntryVM);
        }
    }
    #endregion

    #region save transaction post method
    public async Task<IActionResult> SaveTransactionEntry(TransactionEntryViewModel transactionEntryVM)
    {
        ViewResponseModel partialViewResponseModel = new();
        if (transactionEntryVM.PartyId == 0)
        {
            return Json(await PartialViewResponse("Party/_SaveTransactionEntryModal.cshtml", transactionEntryVM, Messages.ExceptionMessage, ErrorType.Error));
        }
        else
        {
            ApplicationUser user = GetCurrentUserIdentity();
            if (user == null)
            {
                partialViewResponseModel.RedirectUrl = Url.Action("Login", "Login");
                return Json(partialViewResponseModel);
            }
            Businesses business = GetBusinessFromToken();
            if (business == null)
            {
                partialViewResponseModel.RedirectUrl = Url.Action("Index", "Business");
                return Json(partialViewResponseModel);
            }
            transactionEntryVM.BusinessName = business.BusinessName;
            transactionEntryVM.TransactionType = (byte)transactionEntryVM.TransactionTypeEnum;
            int transactionId = await _partyService.SaveTransactionEntry(transactionEntryVM, user.Id);

            if (transactionId != 0)
            {
                if (transactionEntryVM.TransactionId == 0)
                {
                    transactionEntryVM.TransactionId = transactionId;
                    return Json(await PartialViewResponse("Party/_SaveTransactionEntryModal.cshtml", transactionEntryVM, string.Format(Messages.GlobalAddUpdateMesage, "Transaction", "added")));
                }
                else
                {
                    transactionEntryVM.TransactionId = transactionId;
                    return Json(await PartialViewResponse("Party/_SaveTransactionEntryModal.cshtml", transactionEntryVM, string.Format(Messages.GlobalAddUpdateMesage, "Transaction", "updated")));
                }
            }
            else
            {
                if (transactionEntryVM.TransactionId == 0)
                {
                    transactionEntryVM.TransactionId = transactionId;
                    return Json(await PartialViewResponse("Party/_SaveTransactionEntryModal.cshtml", transactionEntryVM, string.Format(Messages.GlobalAddUpdateFailMessage, "add", "transaction")));
                }
                else
                {
                    transactionEntryVM.TransactionId = transactionId;
                    return Json(await PartialViewResponse("Party/_SaveTransactionEntryModal.cshtml", transactionEntryVM, string.Format(Messages.GlobalAddUpdateFailMessage, "update", "transaction")));
                }
            }
        }
    }
    #endregion

    #region delete transaction
    public IActionResult DeleteTransaction(int transactionId)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        int partyId = _partyService.DeleteTransaction(transactionId, user.Id);
        if (partyId != 0)
        {
            return Json(new { success = true, message = string.Format(Messages.GlobalAddUpdateMesage, "Transaction", "deleted"), partyId = partyId });
        }
        else
        {
            return Json(new { success = false, message = string.Format(Messages.GlobalAddUpdateFailMessage, "delete", "transaction"), partyId = partyId });
        }
    }
    #endregion


    #region  total amount base on partytype
    public async Task<IActionResult> GetTotalAmount()
    {
        ViewResponseModel viewResponseModel = new();
        string partyType = _cookieService.GetCookie(Request, TokenKey.PartyType);
        Businesses business = GetBusinessFromToken();
        if (business == null)
        {
            return RedirectToAction("Index", "Business");
        }
        List<PartyViewModel> parties = _partyService.GetPartiesByType(partyType, business.Id, "", "-1", "-1");

        TotalAmountViewModel totalAmountViewModel = new();
        totalAmountViewModel.AmountToGet = 0;
        totalAmountViewModel.AmountToGive = 0;
        if (parties.Count != 0)
        {
            foreach (PartyViewModel party in parties)
            {
                if (party.TransactionType == EnumHelper.TransactionType.GAVE)
                {
                    totalAmountViewModel.AmountToGet += (decimal)party.Amount;
                }
                else
                {
                    totalAmountViewModel.AmountToGive += (decimal)party.Amount;

                }
            }
        }
        viewResponseModel = await PartialViewResponse("Party/_DisplayTotalAmount.cshtml", totalAmountViewModel);
        return Json(viewResponseModel);
    }
    #endregion

    #region Send reminders to party for pending payment
    public IActionResult SendReminderToParty(decimal netBalance, int partyId)
    {
        PartyViewModel party = _partyService.GetPartyById(partyId);
        BusinessItem business = _businessService.GetBusinessItemById(party.BusinessId);
        _ = CommonMethods.Sendreminder(party.Email, party.PartyTypeString, business.BusinessName, netBalance);
        return Json(new { success = true, message = Messages.ReminderSentMessage });
    }
    #endregion

    #region settle up party balance
    public async Task<IActionResult> SettleUpParty(decimal netBalance, int partyId)
    {
        ApplicationUser user = GetCurrentUserIdentity();
        if (user == null)
            return RedirectToAction("Index", "Business");
        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Business");
        TransactionEntryViewModel transactionEntryVM = new();
        transactionEntryVM.PartyId = partyId;
        transactionEntryVM.TransactionAmount = Math.Abs(netBalance);
        transactionEntryVM.BusinessName = business.BusinessName;
        transactionEntryVM.IsSettleup = true;
        if (netBalance < 0)
        {//entry in Gave
            transactionEntryVM.TransactionType = (byte)EnumHelper.TransactionType.GAVE;
        }
        else
        { //got
            transactionEntryVM.TransactionType = (byte)EnumHelper.TransactionType.GOT;
        }
        int transactionId = await _partyService.SaveTransactionEntry(transactionEntryVM, user.Id);
        if (transactionId != 0)
            return Json(new { success = true, message = Messages.SettleUpMessage, partyId = partyId });
        else
            return Json(new { success = false, message = Messages.SettleUpFailMessage, partyId = partyId });
    }
    #endregion

    private Businesses GetBusinessFromToken()
    {
        string token = Request.Cookies[TokenKey.BusinessToken];
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }
        Businesses business = _businessService.GetBusinessFromToken(token);
        return business;
    }
}