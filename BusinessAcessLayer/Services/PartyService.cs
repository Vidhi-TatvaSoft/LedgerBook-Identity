using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Transactions;
using BusinessAcessLayer.Constant;
using BusinessAcessLayer.Interface;
using DataAccessLayer.Constant;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace BusinessAcessLayer.Services;

public class PartyService : IPartyService
{
    private readonly LedgerBookDbContext _context;
    private readonly IAddressService _addressService;
    private readonly IReferenceDataEntityService _referenceDataEntityService;
    private readonly IGenericRepo _genericRepository;
    private readonly IActivityLogService _activityLogService;
    private readonly IBusinessService _businessService;
    private readonly IUserService _userService;

    public PartyService(LedgerBookDbContext context,
    IAddressService addressService,
    IReferenceDataEntityService referenceDataEntityService,
    IGenericRepo genericRepository,
    IActivityLogService activityLogService,
    IBusinessService businessService,
    IUserService userService
    )
    {
        _context = context;
        _addressService = addressService;
        _referenceDataEntityService = referenceDataEntityService;
        _genericRepository = genericRepository;
        _activityLogService = activityLogService;
        _businessService = businessService;
        _userService = userService;
    }

    public List<PartyViewModel> GetPartiesByType(string partyType, int businessId, string searchText, string? filter = "-1", string? sort = "-1")
    {
        int partyTypeId = _genericRepository.Get<ReferenceDataValues>(x => x.EntityType.EntityType == ConstantVariables.PartyType && x.EntityValue == partyType,
         includes: new List<Expression<Func<ReferenceDataValues, object>>>
            {
                x => x.EntityType
            })!.Id;

        List<Parties> allParties = _genericRepository.GetAll<Parties>(x => x.PartyTypId == partyTypeId && x.BusinessId == businessId && x.DeletedAt == null).ToList();
        List<PartyViewModel> partyList = new();

        foreach (Parties party in allParties)
        {
            PartyViewModel partyViewModel = new();
            partyViewModel.PartyId = party.Id;
            partyViewModel.PartyName = party.PartyName;
            partyViewModel.Email = party.Email;
            partyViewModel.PartyTypId = party.PartyTypId;
            partyViewModel.CreatedAt = party.CreatedAt;
            partyViewModel.UpdatedAt = party.UpdatedAt;
            partyViewModel.Amount = _genericRepository.GetAll<LedgerTransactions>(x => x.PartyId == party.Id && x.DeletedAt == null && x.TransactionType == (byte)EnumHelper.TransactionType.GAVE).Sum(x => x.Amount) - _genericRepository.GetAll<LedgerTransactions>(x => x.PartyId == party.Id && x.DeletedAt == null && x.TransactionType == (byte)EnumHelper.TransactionType.GOT).Sum(x => x.Amount);

            partyViewModel.TransactionType = partyViewModel.Amount < 0 ? EnumHelper.TransactionType.GOT : EnumHelper.TransactionType.GAVE;

            partyViewModel.GSTIN = party.GSTIN == null ? null : party.GSTIN;
            partyViewModel.Address = party.AddressId == null ? null : _addressService.GetAddressById((int)party.AddressId);
            partyViewModel.BusinessId = party.BusinessId;
            partyList.Add(partyViewModel);
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            string lowerSearchTerm = searchText.ToLower();
            partyList = partyList.Where(x =>
                x.PartyName.ToLower().Contains(lowerSearchTerm) ||
                x.Email.ToLower().Contains(lowerSearchTerm)
            ).ToList();
        }

        switch (filter)
        {
            case "-1":
                partyList = partyList;
                break;
            case "All":
                partyList = partyList;
                break;
            case "Give":
                partyList = partyList.Where(x => x.Amount < 0).ToList();
                break;
            case "Get":
                partyList = partyList.Where(x => x.Amount > 0).ToList();
                break;
            case "Settled":
                partyList = partyList.Where(x => x.Amount == 0).ToList();
                break;
        }

        switch (sort)
        {
            case "-1":
                partyList = partyList;
                break;
            case "mostRecent":
                partyList = partyList.OrderByDescending(x => x.CreatedAt).ToList();
                break;
            case "HighestAmount":
                partyList = partyList.OrderByDescending(x => Math.Abs((decimal)x.Amount)).ToList();
                break;
            case "LeastAmount":
                partyList = partyList.OrderBy(x => Math.Abs((decimal)x.Amount)).ToList();
                break;
            case "ByName":
                partyList = partyList.OrderBy(x => x.PartyName).ToList();
                break;
            case "Oldest":
                partyList = partyList.OrderBy(x => x.CreatedAt).ToList();
                break;
        }
        return partyList;
    }

    public async Task<int> SavePartyDetails(PartyViewModel partyViewModel, int userId, string partyType)
    {
        string businessName = _businessService.GetBusinessNameById(partyViewModel.BusinessId);
        string userName = _userService.GetuserNameById(userId);
        if (partyViewModel.PartyId == 0)
        {
            Parties party = new();
            party.PartyName = partyViewModel.PartyName;
            party.BusinessId = partyViewModel.BusinessId;
            party.PartyTypId = _genericRepository.Get<ReferenceDataValues>(x => x.EntityType.EntityType == ConstantVariables.PartyType && x.EntityValue == partyType,
                includes: new List<Expression<Func<ReferenceDataValues, object>>>
                {
                    x => x.EntityType
                })!.Id;
            // _referenceDataValueRepo.Get(x => x.EntityType.EntityType == "PartyType" && x.EntityValue == partyType).Id;
            party.Email = partyViewModel.Email.ToLower().Trim();
            party.CreatedAt = DateTime.UtcNow;
            party.CreatedById = userId;
            party.IsEmailVerified = false;
            Guid guid = Guid.NewGuid();
            party.VerificationToken = guid.ToString();
            await _genericRepository.AddAsync<Parties>(party);
            string message = string.Format(Messages.PartyActivity, partyType, party.PartyName, "added ", businessName, userName);
            await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Add, EnumHelper.ActivityEntityType.Business, party.BusinessId, userId, EnumHelper.ActivityEntityType.Party, party.Id);
            return party.Id;
        }
        else
        {
            Parties party = _genericRepository.Get<Parties>(x => x.Id == partyViewModel.PartyId && x.DeletedAt == null);
            party.PartyName = partyViewModel.PartyName;
            party.BusinessId = partyViewModel.BusinessId;
            party.PartyTypId = _genericRepository.Get<ReferenceDataValues>(x => x.EntityType.EntityType == ConstantVariables.PartyType && x.EntityValue == partyType,
                includes: new List<Expression<Func<ReferenceDataValues, object>>>
                    {
                        x => x.EntityType
                    })!.Id;
            //  _referenceDataValueRepo.Get(x => x.EntityType.EntityType == "PartyType" && x.EntityValue == partyType).Id;
            if (partyViewModel.IsEmailChaneged)
            {
                party.IsEmailVerified = false;
                Guid guid = Guid.NewGuid();
                party.VerificationToken = guid.ToString();
            }
            party.Email = partyViewModel.Email.ToLower().Trim();
            party.UpdatedAt = DateTime.UtcNow;
            party.UpdatedById = userId;
            await _genericRepository.UpdateAsync<Parties>(party);
            string message = string.Format(Messages.PartyUpdateActivity, partyType, party.PartyName, businessName, "updated ", userName);
            await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Update, EnumHelper.ActivityEntityType.Business, party.BusinessId, userId, EnumHelper.ActivityEntityType.Party, party.Id);
            return party.Id;
        }
    }

    public string GetEmailVerifiactionTokenForParty(int partyId)
    {
        return _genericRepository.Get<Parties>(x => x.Id == partyId && !x.DeletedAt.HasValue).VerificationToken;
    }

    public async Task<bool> PartyEmailVerification(PartyVerifiedViewModel partyVerifiedViewModel)
    {
        Parties party = _genericRepository.Get<Parties>(x => x.Email.ToLower().Trim() == partyVerifiedViewModel.Email.ToLower().Trim() && x.VerificationToken == partyVerifiedViewModel.Token && x.Id == partyVerifiedViewModel.PartyId && !x.DeletedAt.HasValue);
        if (party != null)
        {
            party.IsEmailVerified = true;
            await _genericRepository.UpdateAsync<Parties>(party);
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool IsPartyverified(int partyId)
    {
        return _genericRepository.IsPresentAny<Parties>(x => x.Id == partyId && !x.DeletedAt.HasValue && x.IsEmailVerified);
    }

    public bool IsEmailChanged(PartyViewModel partyViewModel)
    {
        Parties party = _genericRepository.Get<Parties>(x => x.Id == partyViewModel.PartyId && !x.DeletedAt.HasValue);
        if (party != null)
        {
            if (party.Email.ToLower().Trim() != partyViewModel.Email.ToLower().Trim())
            {
                return true;
            }
        }
        return false;
    }

    public async Task<int> SaveTransactionEntry(TransactionEntryViewModel transactionEntryViewModel, int userId)
    {

        if (transactionEntryViewModel.PartyId == 0 || transactionEntryViewModel.TransactionType == null)
        {
            return 0;
        }

        PartyViewModel partyViewModel = GetPartyById(transactionEntryViewModel.PartyId);
        string userName = _userService.GetuserNameById(userId);
        if (transactionEntryViewModel.TransactionId == 0)
        {
            //add
            LedgerTransactions ledgerTransaction = new();
            ledgerTransaction.PartyId = transactionEntryViewModel.PartyId;
            ledgerTransaction.Amount = transactionEntryViewModel.TransactionAmount;
            ledgerTransaction.TransactionType = transactionEntryViewModel.TransactionType;
            ledgerTransaction.DueDate = transactionEntryViewModel.DueDate == null ? null : transactionEntryViewModel.DueDate;
            ledgerTransaction.Description = transactionEntryViewModel.Description == null ? null : transactionEntryViewModel.Description;
            ledgerTransaction.PaymentMethodId = transactionEntryViewModel.PaymentMethodId == null ? null : transactionEntryViewModel.PaymentMethodId;
            ledgerTransaction.IsSettled = transactionEntryViewModel.IsSettleup;
            ledgerTransaction.CreatedAt = DateTime.UtcNow;
            ledgerTransaction.CreatedById = userId;


            await _genericRepository.AddAsync<LedgerTransactions>(ledgerTransaction);
            string message;
            if (transactionEntryViewModel.IsSettleup)
            {
                message = string.Format(Messages.TransactionActivity, partyViewModel.PartyTypeString, partyViewModel.PartyName, transactionEntryViewModel.BusinessName, "mark as paid", userName);
            }
            else
            {
                message = string.Format(Messages.TransactionAddActivity, partyViewModel.PartyTypeString, partyViewModel.PartyName, ledgerTransaction.Amount.ToString(), transactionEntryViewModel.BusinessName, userName);

                // if (ledgerTransaction.TransactionType == (byte)EnumHelper.TransactionType.GAVE)
                // {
                //     message = string.Format(Messages.TransactionAddGaveMessage,  ledgerTransaction.Amount.ToString(), transactionEntryViewModel.BusinessName, partyViewModel.PartyName);
                // }
                // else
                // {
                //     message = string.Format(Messages.TransactionAddGotMessage, ledgerTransaction.Amount.ToString(), transactionEntryViewModel.BusinessName, partyViewModel.PartyName);
                // }
            }
            await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Add, EnumHelper.ActivityEntityType.Business, partyViewModel.BusinessId, userId, EnumHelper.ActivityEntityType.Transaction, ledgerTransaction.Id);
            return ledgerTransaction.Id;
        }
        else
        {
            //update
            LedgerTransactions ledgerTransaction = _genericRepository.Get<LedgerTransactions>(x => x.Id == transactionEntryViewModel.TransactionId && x.DeletedAt == null);
            if (ledgerTransaction != null)
            {
                if (ledgerTransaction.IsSettled)
                {
                    if (ledgerTransaction.Amount == transactionEntryViewModel.TransactionAmount && ledgerTransaction.TransactionType == transactionEntryViewModel.TransactionType)
                    {
                        transactionEntryViewModel.IsSettleup = true;
                    }
                    else
                    {
                        transactionEntryViewModel.IsSettleup = false;
                    }
                }
                else
                {
                    transactionEntryViewModel.IsSettleup = false;
                }
                ledgerTransaction.Amount = transactionEntryViewModel.TransactionAmount;
                ledgerTransaction.TransactionType = transactionEntryViewModel.TransactionType;
                ledgerTransaction.DueDate = transactionEntryViewModel.DueDate == null ? null : transactionEntryViewModel.DueDate;
                ledgerTransaction.Description = transactionEntryViewModel.Description == null ? null : transactionEntryViewModel.Description;
                ledgerTransaction.PaymentMethodId = transactionEntryViewModel.PaymentMethodId == null ? null : transactionEntryViewModel.PaymentMethodId;
                ledgerTransaction.IsSettled = transactionEntryViewModel.IsSettleup;
                ledgerTransaction.UpdatedAt = DateTime.UtcNow;
                ledgerTransaction.UpdatedById = userId;

                await _genericRepository.UpdateAsync<LedgerTransactions>(ledgerTransaction);
                string message = string.Format(Messages.TransactionActivity, partyViewModel.PartyTypeString, partyViewModel.PartyName, transactionEntryViewModel.BusinessName, "updated", userName);

                // if (ledgerTransaction.TransactionType == (byte)EnumHelper.TransactionType.GAVE)
                // {
                //     message = string.Format(Messages.TransactionUpdateGAVEMessage, ledgerTransaction.Amount.ToString(), transactionEntryViewModel.BusinessName, partyViewModel.PartyName);
                // }
                // else
                // {
                //     message = string.Format(Messages.TransactionUpdateGOTMessage, ledgerTransaction.Amount.ToString(), transactionEntryViewModel.BusinessName, partyViewModel.PartyName);
                // }
                await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Update, EnumHelper.ActivityEntityType.Business, partyViewModel.BusinessId, userId, EnumHelper.ActivityEntityType.Transaction, ledgerTransaction.Id);
                return ledgerTransaction.Id;
            }
            else
            {
                return 0;
            }

        }
    }

    public PartyViewModel GetPartyById(int partyId)
    {
        Parties party = _genericRepository.Get<Parties>(x => x.Id == partyId && x.DeletedAt == null);
        if (party == null)
        {
            return new PartyViewModel();
        }
        else
        {
            PartyViewModel partyViewModel = new();
            partyViewModel.PartyId = party.Id;
            partyViewModel.PartyName = party.PartyName;
            partyViewModel.Email = party.Email;
            partyViewModel.PartyTypId = party.PartyTypId;
            partyViewModel.PartyTypeString = _referenceDataEntityService.GetReferenceValueById(partyViewModel.PartyTypId);
            partyViewModel.GSTIN = party.GSTIN == null ? null : party.GSTIN;
            partyViewModel.Address = party.AddressId == null ? null : _addressService.GetAddressById((int)party.AddressId);
            partyViewModel.BusinessId = party.BusinessId;
            return partyViewModel;
        }
    }

    public decimal GetBalanceTillDate(int partyId, DateTime date)
    {
        decimal amount = 0;
        List<LedgerTransactions> allEntriesOfParty = _genericRepository.GetAll<LedgerTransactions>(x => x.PartyId == partyId && !x.DeletedAt.HasValue).ToList();
        foreach (LedgerTransactions entry in allEntriesOfParty)
        {
            if (entry.UpdatedAt != null)
            {
                if (entry.UpdatedAt <= date)
                {
                    if (entry.TransactionType == (byte)EnumHelper.TransactionType.GAVE)
                    {
                        amount -= entry.Amount;
                    }
                    else
                    {
                        amount += entry.Amount;
                    }
                }

            }
            else
            {
                if (entry.CreatedAt <= date)
                {
                    if (entry.TransactionType == (byte)EnumHelper.TransactionType.GAVE)
                    {
                        amount -= entry.Amount;
                    }
                    else
                    {
                        amount += entry.Amount;
                    }
                }
            }
        }
        return amount;
    }

    public List<TransactionEntryViewModel> GetTransactionsByPartyId(int partyId)
    {
        List<TransactionEntryViewModel> EntriesList = _genericRepository.GetAll<LedgerTransactions>(x => x.PartyId == partyId && !x.DeletedAt.HasValue)
                        .Select(x => new TransactionEntryViewModel
                        {
                            TransactionId = x.Id,
                            PartyId = x.PartyId,
                            TransactionAmount = x.Amount,
                            TransactionType = x.TransactionType,
                            CreatedAt = x.CreatedAt,
                            UpdatedAt = x.UpdatedAt,
                            Balance = 0,
                            Description = x.Description,
                            DueDate = x.DueDate
                        }).OrderByDescending(x => x.CreatedAt).ToList();

        List<LedgerTransactions> allEntriesOfParty = _genericRepository.GetAll<LedgerTransactions>(x => x.PartyId == partyId && !x.DeletedAt.HasValue).ToList();
        for (int i = 0; i < EntriesList.Count; i++)
        {
            decimal amount = 0;
            foreach (LedgerTransactions entry in allEntriesOfParty)
            {
                if (entry.CreatedAt <= EntriesList[i].CreatedAt)
                {
                    if (entry.TransactionType == (byte)EnumHelper.TransactionType.GAVE)
                    {
                        amount -= entry.Amount;
                    }
                    else
                    {
                        amount += entry.Amount;
                    }
                }
            }
            EntriesList[i].Balance = amount;
        }
        return EntriesList;
    }

    public TransactionEntryViewModel GetTransactionbyTransactionId(int transactionId)
    {
        return _genericRepository.GetAll<LedgerTransactions>(x => x.Id == transactionId && !x.DeletedAt.HasValue).Select(x => new TransactionEntryViewModel
        {
            TransactionId = x.Id,
            PartyId = x.PartyId,
            TransactionAmount = x.Amount,
            TransactionType = x.TransactionType,
            CreatedAt = x.CreatedAt,
            Description = x.Description
        }).ToList().FirstOrDefault();
    }

    public int DeleteTransaction(int transactionId, int userId)
    {
        LedgerTransactions transaction = _genericRepository.Get<LedgerTransactions>(t => t.Id == transactionId && !t.DeletedAt.HasValue);
        PartyViewModel partyViewModel = GetPartyById(transaction.PartyId);
        string businessname = _businessService.GetBusinessNameById(partyViewModel.BusinessId);
        string userName = _userService.GetuserNameById(userId);
        if (transaction != null)
        {
            transaction.DeletedAt = DateTime.UtcNow;
            transaction.DeletedById = userId;
            _genericRepository.Update<LedgerTransactions>(transaction);
            string message = string.Format(Messages.TransactionActivity, partyViewModel.PartyTypeString, partyViewModel.PartyName, businessname, "deleted", userName);
            _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Delete, EnumHelper.ActivityEntityType.Business, partyViewModel.BusinessId, userId, EnumHelper.ActivityEntityType.Transaction, transaction.Id);
            return transaction.PartyId;
        }
        else
        {
            return 0;
        }
    }

    public List<Parties> GetAllPartiesByBusiness(int businessId, int userId)
    {
        if (businessId != 0)
            return _genericRepository.GetAll<Parties>(x => x.BusinessId == businessId).ToList();
        else
        {
            int OwnerRoleId = _genericRepository.Get<Role>(x => x.RoleName == ConstantVariables.OwnerRole).Id;
            List<int> businessIds = _genericRepository.GetAll<UserBusinessMappings>(x => x.UserId == userId && x.RoleId == OwnerRoleId && (x.CreatedById == userId || (!x.DeletedAt.HasValue && x.IsActive))).Select(x => x.BusinessId).Distinct().ToList();
            List<Parties> parties = new();
            foreach (int id in businessIds)
            {
                List<Parties> partiesTemp = _genericRepository.GetAll<Parties>(x => x.BusinessId == id).ToList();
                parties = parties.Concat(partiesTemp).ToList();
            }
            return parties;
        }
    }
}