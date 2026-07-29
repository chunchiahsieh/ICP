using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class MassUpdateNonNcpiController : MassUpdateControllerBase
{
    public MassUpdateNonNcpiController(
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer,
        MassUpdateImportService importService,
        MassUpdatePendingFileStore pendingFileStore,
        ILogger<MassUpdateNonNcpiController> logger)
        : base(environment, localizer, importService, pendingFileStore, logger)
    {
    }

    protected override string ViewPath => "~/Views/FUNCTION/MassUpdateNonNcpi/View.cshtml";
}
