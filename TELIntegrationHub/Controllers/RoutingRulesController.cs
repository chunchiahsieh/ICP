using Microsoft.AspNetCore.Mvc;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Controllers;

[ApiController]
[Route("api/routing-rules")]
public class RoutingRulesController : ControllerBase
{
    private readonly IRoutingService _routingService;

    public RoutingRulesController(IRoutingService routingService)
        => _routingService = routingService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _routingService.GetAllAsync(cancellationToken));
}
