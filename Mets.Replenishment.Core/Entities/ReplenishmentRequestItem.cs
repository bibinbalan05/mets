using System;

namespace Mets.Replenishment.Core.Entities;

public class ReplenishmentRequestItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string ArticleNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int FulfilledQuantity { get; set; } = 0;
    public int? StockAvailable { get; set; }
}
