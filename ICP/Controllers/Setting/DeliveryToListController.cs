using ICP;
using ICP.Data;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public class DeliveryToListController : SystemConfigControllerBase
{
    public DeliveryToListController(ApplicationDbContext icpDb, IStringLocalizer<SharedResource> localizer)
        : base(icpDb, localizer)
    {
    }

    protected override string Category => "DeliveryToList";

    protected override void ApplyEditModel(SystemConfig entity, SystemConfigEditModel model)
    {
        entity.Key1 = model.Key1!.Trim();
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
            Key1 = entity.Key1,
            Value1 = entity.Value1,
            Value2 = entity.Value2,
            Value3 = entity.Value3,
            Value4 = entity.Value4,
            Value5 = entity.Value5,
            Value6 = entity.Value6
        };
    }
}
