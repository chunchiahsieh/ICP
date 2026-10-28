using ICP;
using ICP.Data;
using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public class CustomizedController : SystemConfigControllerBase
{
    private static readonly string[] ExcludedCategories = SettingCategories.CustomizedExcluded;
    private readonly PageDataScopeService _dataScope;

    public CustomizedController(
        ApplicationDbContext icpDb,
        IStringLocalizer<SharedResource> localizer,
        PageDataScopeService dataScope)
        : base(icpDb, localizer)
    {
        _dataScope = dataScope;
    }

    protected override string Category => "Customized";

    protected override IQueryable<SystemConfig> ScopeQuery(IQueryable<SystemConfig> query)
    {
        var customizedQuery = query.Where(e => e.Category != null
            && e.Category != ""
            && !ExcludedCategories.Contains(e.Category));
        return _dataScope.Apply(customizedQuery);
    }

    protected override IQueryable<SystemConfig> ReplacementScopeQuery(IQueryable<SystemConfig> query)
    {
        var customizedQuery = query.Where(e => e.Category != null
            && e.Category != ""
            && !ExcludedCategories.Contains(e.Category));
        return _dataScope.Apply(customizedQuery);
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
        entity.FunctionCode = OptionalText(model.FunctionCode);
        entity.Key1 = model.Key1!.Trim();
        entity.Key2 = model.Key2?.Trim() ?? string.Empty;
        entity.Value1 = OptionalText(model.Value1);
        entity.Value2 = OptionalText(model.Value2);
        entity.Value3 = OptionalText(model.Value3);
        entity.Value4 = OptionalText(model.Value4);
        entity.Value5 = OptionalText(model.Value5);
        entity.Value6 = OptionalText(model.Value6);
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
