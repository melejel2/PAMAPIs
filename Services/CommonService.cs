using Microsoft.EntityFrameworkCore;
using PAMAPIs.Data;
using static PAMAPIs.Data.PAMContext;

namespace PAMAPIs.Services
{
    public interface ICommonService
    {
        string GetSiteCode(int siteId);
    }

    public class CommonService : ICommonService
    {
        private readonly PAMContext _context;

        public CommonService(PAMContext context)
        {
            _context = context;
        }

        public string GetSiteCode(int siteId)
        {
            // Implement the logic to get site code
            return _context.Sites.FirstOrDefault(s => s.SiteId == siteId)?.SiteCode ?? string.Empty;
        }
    }
}
