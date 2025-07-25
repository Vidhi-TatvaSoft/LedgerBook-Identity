using BusinessAcessLayer.Constant;
using BusinessAcessLayer.Interface;
using DataAccessLayer.Constant;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;

namespace LedgerBook.Controllers;

public class ReportsController : BaseController
{
    private readonly ITransactionReportSevice _transactionReportService;
    private readonly IBusinessService _businessService;
    private readonly IPartyService _partyService;

    public ReportsController(
        ILoginService loginService,
        IViewRenderService viewRenderService,
        ITransactionReportSevice transactionReportSevice,
        IBusinessService businessService,
        IPartyService partyService,
        IActivityLogService activityLogService
        ) : base(loginService, viewRenderService, activityLogService)
    {
        _transactionReportService = transactionReportSevice;
        _businessService = businessService;
        _partyService = partyService;
    }


    #region index page of reports
    public IActionResult Reports()
    {
        IActionResult redirectResult = RedirectToLoginIfNotAuthenticated();
        if (redirectResult != null)
        {
            return redirectResult;
        }
        ViewData["report"] = "transaction";
        ViewData["sidebar"] = "Reports";
        return View();
    }
    #endregion

    #region TransactionReportsPartial to display counts of party
    public IActionResult TransactionReportsPartial()
    {
        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Business");
        ReportCountsViewModel reportCountsVM = _transactionReportService.GetReportCounts(business.Id);
        return PartialView("_TransactionReportPagePartial", reportCountsVM);
    }
    #endregion

    #region display transaction entries
    public IActionResult DisplayTransactionEntries(string partyType, int searchPartyId = 0, string startDate = "", string endDate = "")
    {
        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Business");
        ReportTransactionEntriesViewModel transactionEntries = new();
        transactionEntries.TransactionsList = _transactionReportService.GetTransactionEntries(business.Id, partyType, searchPartyId, startDate, endDate);

        transactionEntries.youGave = transactionEntries.TransactionsList.Where(x => x.TransactionType == (byte)EnumHelper.TransactionType.GAVE).Sum(x => x.TransactionAmount);
        transactionEntries.YouGot = transactionEntries.TransactionsList.Where(x => x.TransactionType == (byte)EnumHelper.TransactionType.GOT).Sum(x => x.TransactionAmount);
        transactionEntries.NetBalance = transactionEntries.YouGot - transactionEntries.youGave;
        return PartialView("_TransactionEntriesByCustomer", transactionEntries);
    }
    #endregion

    #region  search options 
    public IActionResult SearchPartyOptions(string partytype, string searchText)
    {
        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Business");
        List<PartyViewModel> parties = _partyService.GetPartiesByType(partytype, business.Id, searchText, "-1", "-1");
        return PartialView("_SearchPartyOptionsPartial", parties);
    }
    #endregion

    #region generate pdf
    public IActionResult GenerateReportPdf(string partytype, string timePeriod, int searchPartyId = 0, string startDate = "", string endDate = "")
    {
        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Business");
        ReportTransactionEntriesViewModel reportpdf = _transactionReportService.GetReportdata(partytype, timePeriod, business.Id, searchPartyId, startDate, endDate);
        // return PartialView("_reportpdf", reportpdf);

        ViewAsPdf generatedpdf = new ViewAsPdf("_reportpdf", reportpdf)
        {
            FileName = "TransactionReport_" + reportpdf.Startdate + "_to_" + reportpdf.EndDate + ".pdf",
            PageMargins = new Rotativa.AspNetCore.Options.Margins(0, 0, 0, 0)
        };
        return generatedpdf;
    }
    #endregion

    #region export data to excel
    public async Task<IActionResult> GenerateExcel(string partytype, string timePeriod, int searchPartyId = 0, string startDate = "", string endDate = "")
    {

        Businesses business = GetBusinessFromToken();
        if (business == null)
            return RedirectToAction("Index", "Business");
        ReportTransactionEntriesViewModel reportExcel = _transactionReportService.GetReportdata(partytype, timePeriod, business.Id, searchPartyId, startDate, endDate);

        byte[] FileData = await _transactionReportService.ExportData(reportExcel);
        FileContentResult result = new FileContentResult(FileData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = "TransactionReport_" + reportExcel.Startdate + "_to_" + reportExcel.EndDate + ".xlsx"
        };
        return result;
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
