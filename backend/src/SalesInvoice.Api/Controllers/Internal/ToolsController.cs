using Microsoft.AspNetCore.Mvc;
using SalesInvoice.Application.Tools;
using SalesInvoice.Infrastructure.Tools;

namespace SalesInvoice.Api.Controllers.Internal;

[ApiController]
[Route("internal/tools")]
public class ToolsController(
    ResolveCustomerHandler resolveCustomer,
    GetPurchaseHistoryHandler getPurchaseHistory,
    ValidateInventoryHandler validateInventory,
    ResolveDiscountHandler resolveDiscount,
    BuildDraftHandler buildDraft,
    RecordStepHandler recordStep,
    RecommendProductsHandler recommendProducts,
    SaveRecommendationHandler saveRecommendation) : ControllerBase
{
    [HttpPost("resolve-customer")]
    public async Task<IActionResult> ResolveCustomer([FromBody] ResolveCustomerRequest request)
        => Ok(await resolveCustomer.HandleAsync(request));

    [HttpPost("get-purchase-history")]
    public async Task<IActionResult> GetPurchaseHistory([FromBody] GetPurchaseHistoryRequest request)
        => Ok(await getPurchaseHistory.HandleAsync(request));

    [HttpPost("adjust-quantities")]
    public IActionResult AdjustQuantities([FromBody] AdjustQuantitiesRequest request)
        => Ok(AdjustQuantitiesHandler.Handle(request));

    [HttpPost("validate-inventory")]
    public async Task<IActionResult> ValidateInventory([FromBody] ValidateInventoryRequest request)
        => Ok(await validateInventory.HandleAsync(request));

    [HttpPost("resolve-discount")]
    public async Task<IActionResult> ResolveDiscount([FromBody] ResolveDiscountRequest request)
        => Ok(await resolveDiscount.HandleAsync(request));

    [HttpPost("build-draft")]
    public async Task<IActionResult> BuildDraft([FromBody] BuildDraftRequest request)
        => Ok(await buildDraft.HandleAsync(request));

    [HttpPost("record-step")]
    public async Task<IActionResult> RecordStep([FromBody] RecordStepRequest request)
        => Ok(await recordStep.HandleAsync(request));

    [HttpPost("recommend-products")]
    public async Task<IActionResult> RecommendProducts([FromBody] RecommendProductsRequest request)
        => Ok(await recommendProducts.HandleAsync(request));

    [HttpPost("save-recommendation")]
    public async Task<IActionResult> SaveRecommendation([FromBody] SaveRecommendationRequest request)
        => Ok(await saveRecommendation.HandleAsync(request));
}
