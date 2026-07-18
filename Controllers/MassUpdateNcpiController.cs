using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class MassUpdateNcpiController : MassUpdateControllerBase
{
    public MassUpdateNcpiController(
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer,
        MassUpdateImportService importService,
        MassUpdatePendingFileStore pendingFileStore,
        ILogger<MassUpdateNcpiController> logger)
        : base(environment, localizer, importService, pendingFileStore, logger)
    {
    }

    protected override string ViewPath => "~/Views/FUNCTION/MassUpdateNcpi/View.cshtml";
}
