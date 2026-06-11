using ICP;
using ICP.Data;
using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public class CustomizedController : SystemConfigControllerBase
{
    private static readonly string[] ExcludedCategories = SettingCategories.CustomizedExcluded;

    public CustomizedController(ApplicationDbContext icpDb, IStringLocalizer<SharedResource> localizer)
        : base(icpDb, localizer)
    {
    }

    protected override string Category => "Customized";

    protected override IQueryable<SystemConfig> ScopeQuery(IQueryable<SystemConfig> query)
    {
        return query.Where(e => !e.IsDeleted
            && e.Category != null
            && e.Category != ""
            && !ExcludedCategories.Contains(e.Category));
    }

    protected override void ValidateSaveModel(SystemConfigEditModel model)
    {
        base.ValidateSaveModel(model);

        if (model.Id <= 0)
        {
            if (string.IsNullOrWhiteSpace(model.Category))
            {
                ModelState.AddModelError(nameof(SystemConfigEditModel.Category), Localizer["Setting.SystemConfig.CategoryRequired"]);
            }
            else if (!SettingCategories.IsInCustomizedScope(model.Category))
            {
                ModelState.AddModelError(nameof(SystemConfigEditModel.Category), Localizer["Setting.SystemConfig.CustomizedCategoryNotAllowed"]);
            }
        }
    }

    protected override void ApplyCreateCategory(SystemConfig entity, SystemConfigEditModel model)
    {
        entity.Category = model.Category!.Trim();
    }

    protected override void ApplyEditModel(SystemConfig entity, SystemConfigEditModel model)
    {
        entity.FunctionCode = model.FunctionCode?.Trim();
        entity.Key1 = model.Key1!.Trim();
        entity.Key2 = model.Key2?.Trim() ?? string.Empty;
        entity.Value1 = model.Value1?.Trim();
        entity.Value2 = model.Value2?.Trim();
        entity.Value3 = model.Value3?.Trim();
        entity.Value4 = model.Value4?.Trim();
        entity.Value5 = model.Value5?.Trim();
        entity.Value6 = model.Value6?.Trim();
    }

    protected override SystemConfigEditModel MapToEditModel(SystemConfig entity)
    {
        return new SystemConfigEditModel
        {
            Id = entity.Id,
            Category = entity.Category,
            FunctionCode = entity.FunctionCode,
            Key1 = entity.Key1,
            Key2 = entity.Key2,
            Value1 = entity.Value1,
            Value2 = entity.Value2,
            Value3 = entity.Value3,
            Value4 = entity.Value4,
            Value5 = entity.Value5,
            Value6 = entity.Value6
        };
    }
}
