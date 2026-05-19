using System.Threading.Tasks;

namespace Mets.Replenishment.Core.Interfaces;

public interface IStockValidationService
{
    Task<int> GetAvailableStockAsync(string articleNumber);
}
