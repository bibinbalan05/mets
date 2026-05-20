using System;
using System.Collections.Generic;

namespace Mets.Replenishment.Core.DTOs;

public class FulfillRequestDto
{
    public Dictionary<Guid, int> FulfilledQuantities { get; set; } = new();
}
