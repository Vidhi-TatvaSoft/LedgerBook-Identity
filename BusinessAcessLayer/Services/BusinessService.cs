
using BusinessAcessLayer.Interface;
using DataAccessLayer.Models;
using DataAccessLayer.ViewModels;
using DataAccessLayer.Constant;
using BusinessAcessLayer.Helper;

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Utilities;
using System.Transactions;
using BusinessAcessLayer.Constant;
using System.Linq.Expressions;
using System.Security.Claims;

namespace BusinessAcessLayer.Services;

public class BusinessService : IBusinessService
{
    private readonly LedgerBookDbContext _context;
    private readonly IJWTTokenService _jwttokenService;
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IAttachmentService _attachmentService;
    private readonly IAddressService _addressService;
    private readonly IUserBusinessMappingService _userBusinessMappingService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IGenericRepo _genericRepository;
    private readonly IActivityLogService _activityLogService;


    public BusinessService(LedgerBookDbContext context,
    IJWTTokenService jWTTokenService,
     IUserService userService,
     IRoleService roleService,
     IAttachmentService attachmentService,
     IAddressService addressService,
     IUserBusinessMappingService userBusinessMappingService,
     ITransactionRepository transactionRepository,
     IGenericRepo genericRepository,
     IActivityLogService activityLogService
     )
    {
        _context = context;
        _jwttokenService = jWTTokenService;
        _userService = userService;
        _roleService = roleService;
        _attachmentService = attachmentService;
        _addressService = addressService;
        _userBusinessMappingService = userBusinessMappingService;
        _transactionRepository = transactionRepository;
        _genericRepository = genericRepository;
        _activityLogService = activityLogService;
    }


    public List<BusinessViewModel> GetBusinesses(int userId, string searchText = null)
    {
        IEnumerable<UserBusinessMappings> query = _genericRepository.GetAll<UserBusinessMappings>(predicate: ubm => ubm.UserId == userId && ubm.DeletedAt == null && ubm.IsActive,
                thenIncludes: new List<Func<IQueryable<UserBusinessMappings>, IQueryable<UserBusinessMappings>>>
                {
                   x => x.Include(ubm => ubm.Business)
                            .ThenInclude(b => b.LogoAttachment)
                        .Include(ubm => ubm.Business)
                            .ThenInclude(b => b.BusinessCategory)
                        .Include(ubm => ubm.Business)
                            .ThenInclude(b => b.BusinessType)
                        .Include(ubm => ubm.User)
                });

        // search filter
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = searchText.Trim().ToLower();
            query = query.Where(ubm =>
                ubm.Business.BusinessName.ToLower().Contains(searchText) ||
                ubm.Business.BusinessCategory.EntityValue.ToLower().Contains(searchText) ||
                ubm.Business.BusinessType.EntityValue.ToLower().Contains(searchText) ||
                _genericRepository.GetAll<UserBusinessMappings>(x => !x.DeletedAt.HasValue,
                 includes: new List<Expression<Func<UserBusinessMappings, object>>>
                {
                    x => x.User,
                    x => x.Role
                }).Any(x => x.BusinessId == ubm.BusinessId &&
                             x.Role.RoleName == Constant.ConstantVariables.OwnerRole &&
                             x.DeletedAt == null &&
                             (x.User.FirstName + " " + x.User.LastName).ToLower().Contains(searchText))
            );
        }

        var businesses = query
            .GroupBy(ubm => ubm.BusinessId)
            .ToList();

        return businesses.Select(data =>
        {
            UserBusinessMappings business = data.First();
            List<string> ownerNames = _genericRepository.GetAll<UserBusinessMappings>(predicate: x => x.BusinessId == data.Key && x.Role.RoleName == Constant.ConstantVariables.OwnerRole && x.DeletedAt == null && x.IsActive,
                    includes: new List<Expression<Func<UserBusinessMappings, object>>>
                    {
                        x => x.User
                    }
                    ).Select(x => x.User.FirstName + " " + x.User.LastName)
                        .ToList();

            List<int> ownerIds = _genericRepository.GetAll<UserBusinessMappings>(predicate: x => x.BusinessId == data.Key && x.Role.RoleName == Constant.ConstantVariables.OwnerRole && x.DeletedAt == null && x.IsActive,
                    includes: new List<Expression<Func<UserBusinessMappings, object>>>
                    {
                        x => x.User
                    }
                    ).Select(x => x.User.Id)
                    .ToList();

            return new BusinessViewModel
            {
                BusinessId = business.Business.Id,
                BusienssName = business.Business.BusinessName,
                LogoPath = business.Business.LogoAttachment?.Path,
                OwnerId = string.Join(", ", ownerIds),
                OwnerName = string.Join(", ", ownerNames),
                CurentUserId = userId,
                CanEditDelete = false,
                CanDelete = false
            };
        })
        .ToList();
    }

    public List<BusinessViewModel> GetRolewiseBusiness(List<BusinessViewModel> businessList)
    {
        if (businessList != null)
        {
            foreach (BusinessViewModel business in businessList)
            {
                List<UserBusinessMappings> mainOwner = _genericRepository.GetAll<UserBusinessMappings>(x => x.BusinessId == business.BusinessId && x.UserId == x.CreatedById && !x.DeletedAt.HasValue).ToList();
                if (mainOwner.Count != 0)
                {
                    int mainOwnerId = mainOwner.FirstOrDefault().UserId;
                    string[] userIdstring = business.OwnerId.Split(",");
                    List<int> userIds = new();
                    foreach (string id in userIdstring)
                    {
                        userIds.Add(Int32.Parse(id));
                    }
                    if (userIds.Contains(business.CurentUserId))
                    {
                        business.CanEditDelete = true;
                    }
                    if (business.CurentUserId == mainOwnerId)
                    {
                        business.CanDelete = true;
                    }
                }
            }
        }
        return businessList;
    }

    public async Task<int> SaveBusiness(BusinessItem businessItem, int userId)
    {
        try
        {
            await _transactionRepository.BeginTransactionAsync();
            int addressId = 0;
            int attachmentId = 0;
            int businessId;
            if (businessItem.BusinessLogoAttachment != null)
            {
                if (businessItem.BusinessLogoAttachment.BusinesLogoPath != null)
                {
                    attachmentId = await _attachmentService.SaveAttachment(businessItem.BusinessLogoAttachment, userId);
                }
            }
            if (businessItem.BusinessAddress != null)
            {
                if (businessItem.BusinessAddress.AddressLine1 != null || businessItem.BusinessAddress.AddressLine2 != null || businessItem.BusinessAddress.City != null || businessItem.BusinessAddress.Pincode != null)
                {
                    addressId = await _addressService.SaveAddress(businessItem.BusinessAddress, userId);
                }
                else if (businessItem.BusinessAddress.AddressLine1 == null || businessItem.BusinessAddress.AddressLine2 == null || businessItem.BusinessAddress.City == null || businessItem.BusinessAddress.Pincode == null)
                {
                    addressId = 0;
                }
            }
            if (businessItem.BusinessId == 0)
            {
                Businesses business = new();
                business.BusinessName = businessItem.BusinessName;
                business.MobileNumber = businessItem.MobileNumber;
                if (addressId != 0)
                {
                    business.AddressId = addressId;
                }
                else
                {
                    business.AddressId = null;
                }
                if (attachmentId != 0)
                {
                    business.LogoAttachmentId = attachmentId;
                }
                else
                {
                    business.LogoAttachmentId = null;
                }
                business.BusinessCategoryId = businessItem.BusinescategoryId;
                business.BusinessTypeId = businessItem.BusinessTypeId;
                business.CreatedAt = DateTime.UtcNow;
                business.CreatedById = userId;
                business.IsActive = businessItem.IsActive;
                await _genericRepository.AddAsync<Businesses>(business);
                businessId = business.Id;
                int roleId = _genericRepository.Get<Role>(r => r.RoleName == ConstantVariables.OwnerRole && !r.DeletedAt.HasValue)!.Id;

                UserViewmodel user = _genericRepository.GetAll<ApplicationUser>(x => x.Id == userId && x.DeletedAt == null).Select(x => new UserViewmodel
                {
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    MobileNumber = x.PhoneNumber == null ? 0 : long.Parse(x.PhoneNumber),


                }).FirstOrDefault();
                if (user != null)
                {
                    int personalDetailId = await _userService.SavePersonalDetails(user, userId);
                    int userBusinessmappingOwnerId = await _userBusinessMappingService.SaveUserBusinessMapping(userId, businessId, roleId, personalDetailId, userId);
                }

            }
            else
            {
                Businesses updateBusiness = _genericRepository.Get<Businesses>(b => b.Id == businessItem.BusinessId && b.DeletedAt == null)!;
                if (updateBusiness != null)
                {
                    updateBusiness.BusinessName = businessItem.BusinessName;
                    updateBusiness.MobileNumber = businessItem.MobileNumber;
                    updateBusiness.BusinessCategoryId = businessItem.BusinescategoryId;
                    updateBusiness.BusinessTypeId = businessItem.BusinessTypeId;
                    if (addressId != 0)
                    {
                        updateBusiness.AddressId = addressId;
                    }
                    else
                    {
                        updateBusiness.AddressId = null;
                    }
                    if (attachmentId != 0)
                    {
                        updateBusiness.LogoAttachmentId = attachmentId;
                    }
                    else
                    {
                        updateBusiness.LogoAttachmentId = null;
                    }
                    updateBusiness.UpdatedAt = DateTime.UtcNow;
                    updateBusiness.UpdatedById = userId;
                    updateBusiness.IsActive = businessItem.IsActive;
                    await _genericRepository.UpdateAsync<Businesses>(updateBusiness);
                    businessId = updateBusiness.Id;
                }
                else
                {
                    businessId = 0;
                }
            }
            await _transactionRepository.CommitAsync();
            string userName = _userService.GetuserNameById(userId);
            if (businessItem.BusinessId == 0)
            {
                string message = string.Format(Messages.BusinessActivity, "Business", "created", userName);
                await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Add, EnumHelper.ActivityEntityType.Business, businessId, userId);
            }
            else
            {
                string message = string.Format(Messages.BusinessActivity, "Business", "updated", userName);
                await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Update, EnumHelper.ActivityEntityType.Business, businessId, userId);
            }
            return businessId;
        }
        catch (Exception e)
        {
            await _transactionRepository.RollbackAsync();
            throw;
        }
    }

    public BusinessItem GetBusinessItemById(int? businessId)
    {
        BusinessItem businessViewModel = new();
        Businesses presentBusiness = _genericRepository.Get<Businesses>(b => b.Id == businessId && b.DeletedAt == null)!;
        if (presentBusiness != null)
        {
            businessViewModel.BusinessId = presentBusiness.Id;
            businessViewModel.BusinessName = presentBusiness.BusinessName;
            businessViewModel.BusinescategoryId = presentBusiness.BusinessCategoryId;
            businessViewModel.BusinessTypeId = presentBusiness.BusinessTypeId;
            businessViewModel.MobileNumber = presentBusiness.MobileNumber == null ? 0 : (long)presentBusiness.MobileNumber;
            if (presentBusiness.LogoAttachmentId.HasValue)
            {
                businessViewModel.BusinessLogoAttachment = _attachmentService.GetAttachmentById(presentBusiness.LogoAttachmentId.Value);
            }
            if (presentBusiness.AddressId == null)
            {
                businessViewModel.BusinessAddress = new();
                businessViewModel.BusinessAddress.AddressLine1 = null;
                businessViewModel.BusinessAddress.AddressLine2 = null;
                businessViewModel.BusinessAddress.City = null;
                businessViewModel.BusinessAddress.Pincode = null;
            }
            else
            {
                businessViewModel.BusinessAddress = _addressService.GetAddressById((int)presentBusiness.AddressId);
            }
            businessViewModel.IsActive = presentBusiness.IsActive;
            return businessViewModel;
        }
        return null;
    }

    public Businesses GetBusinessFromToken(string token)
    {
        ClaimsPrincipal claims = _jwttokenService.GetClaimsFromToken(token);
        int businessId = int.Parse(_jwttokenService.GetClaimValue(token, "id"));
        return _genericRepository.Get<Businesses>(b => b.Id == businessId && b.DeletedAt == null);
    }

    public async Task<bool> DeleteBusiness(int businessId, int userId)
    {
        try
        {
            await _transactionRepository.BeginTransactionAsync();
            Businesses business = _genericRepository.Get<Businesses>(b => b.Id == businessId && !b.DeletedAt.HasValue)!;
            if (business != null)
            {
                business.DeletedAt = DateTime.UtcNow;
                business.DeletedById = userId;
                _genericRepository.Update<Businesses>(business);

                List<UserBusinessMappings> userBusinessMappings = _genericRepository.GetAll<UserBusinessMappings>(ubm => ubm.BusinessId == businessId && !ubm.DeletedAt.HasValue).ToList();
                foreach (UserBusinessMappings mapping in userBusinessMappings)
                {
                    PersonalDetails personalDetails = _genericRepository.Get<PersonalDetails>(pd => pd.Id == mapping.PersonDetailId && !pd.DeletedAt.HasValue);
                    if (personalDetails != null)
                    {
                        personalDetails.DeletedAt = DateTime.UtcNow;
                        personalDetails.DeletedById = userId;
                        _genericRepository.Update<PersonalDetails>(personalDetails);
                    }
                    mapping.DeletedAt = DateTime.UtcNow;
                    mapping.DeletedById = userId;
                    _genericRepository.Update<UserBusinessMappings>(mapping);
                }

                List<Parties> parties = _genericRepository.GetAll<Parties>(p => p.BusinessId == businessId && !p.DeletedAt.HasValue).ToList();
                foreach (Parties party in parties)
                {
                    List<LedgerTransactions> transactions = _genericRepository.GetAll<LedgerTransactions>(t => t.PartyId == party.Id && !t.DeletedAt.HasValue).ToList();
                    foreach (LedgerTransactions transaction in transactions)
                    {
                        transaction.DeletedAt = DateTime.UtcNow;
                        transaction.DeletedById = userId;
                        _genericRepository.Update<LedgerTransactions>(transaction);
                    }
                    party.DeletedAt = DateTime.UtcNow;
                    party.DeletedById = userId;
                    _genericRepository.Update<Parties>(party);
                }
                await _genericRepository.SaveChangesAsync();
                await _transactionRepository.CommitAsync();
                string userName = _userService.GetuserNameById(userId);
                string message = string.Format(Messages.BusinessActivity, "Business", "deleted", userName);
                await _activityLogService.SetActivityLog(message, EnumHelper.Actiontype.Delete, EnumHelper.ActivityEntityType.Business, businessId, userId);
                return true;
            }
            else
            {
                return false;
            }

        }
        catch (Exception e)
        {
            await _transactionRepository.RollbackAsync();
            throw;
        }
    }

    public string GetBusinessNameById(int businessId)
    {
        return _genericRepository.Get<Businesses>(x => x.Id == businessId && !x.DeletedAt.HasValue).BusinessName;
    }

    //get all business including deleted business to show activity business drop down
    public List<BusinessViewModel> GetAllBusinesses(int userId)
    {
        int OwnerRoleId = _genericRepository.Get<Role>(x => x.RoleName == ConstantVariables.OwnerRole).Id;
        IEnumerable<UserBusinessMappings> query = _genericRepository.GetAll<UserBusinessMappings>(predicate: ubm => ubm.UserId == userId && ubm.RoleId == OwnerRoleId && (ubm.CreatedById == userId || (!ubm.DeletedAt.HasValue && ubm.IsActive)),
                thenIncludes: new List<Func<IQueryable<UserBusinessMappings>, IQueryable<UserBusinessMappings>>>
                {
                   x => x.Include(ubm => ubm.Business)
                            .ThenInclude(b => b.LogoAttachment)
                        .Include(ubm => ubm.Business)
                            .ThenInclude(b => b.BusinessCategory)
                        .Include(ubm => ubm.Business)
                            .ThenInclude(b => b.BusinessType)
                        .Include(ubm => ubm.User)
                });

        var businesses = query
            .GroupBy(ubm => ubm.BusinessId)
            .ToList();

        return businesses.Select(data =>
                {
                    UserBusinessMappings business = data.First();

                    return new BusinessViewModel
                    {
                        BusinessId = business.Business.Id,
                        BusienssName = business.Business.BusinessName,
                    };
                })
                .ToList();
    }
}