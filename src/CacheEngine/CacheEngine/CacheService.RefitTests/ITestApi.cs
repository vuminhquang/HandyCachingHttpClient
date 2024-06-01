using Refit;
using System.Threading.Tasks;

namespace CacheService.RefitTests;

public interface ITestApi
{
    [Get("/data")]
    Task<string> GetDataAsync();
}