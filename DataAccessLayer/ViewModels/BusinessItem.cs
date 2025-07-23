using System.ComponentModel.DataAnnotations;
using DataAccessLayer.Constant;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Http;

namespace DataAccessLayer.ViewModels;

public class BusinessItem
{
    public int BusinessId { get; set; }

    [Required(ErrorMessage = MessageHelper.BusinessNameRequireMessage)]
    [StringLength(100, ErrorMessage = MessageHelper.BusinessNameLengthMessage)]
    public string BusinessName { get; set; }

    public AttachmentViewModel? BusinessLogoAttachment { get; set; }

    public int BusinescategoryId { get; set; }

    public int BusinessTypeId { get; set; }

    public AddressViewModel? BusinessAddress { get; set; }

    [Required(ErrorMessage = MessageHelper.MobileNumberRequire)]
    [Range(1000000000, 9999999999, ErrorMessage = MessageHelper.MobileNumberlength)]
    public long MobileNumber { get; set; }

    public bool IsActive { get; set; } 
    
    public List<UserViewmodel> Users { get; set; }

    public List<ReferenceDataValues> BusinessCategories { get; set; }

    public List<ReferenceDataValues> BusinessTypes { get; set; }


    public UserViewmodel AddEditUser { get; set; }



}