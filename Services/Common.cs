using PAMAPIs.Data;
using PAMAPIs.Models;
using Microsoft.EntityFrameworkCore;

namespace PAMAPIs.Services
{
    public class Common
    {
        private PAMContext con;
        public Common(PAMContext con) { this.con = con; }
        //protected readonly DbSet<TEntity> _entities;
        public string GetSiteCode(int Id)
        {
            var SiteCode = "";
            var site = con.Sites.Find(Id);
            if (site != null)
            {
                SiteCode = site.SiteCode;
            }
            return SiteCode;
        }

        
        public int GetSupplierId(int POId)
        {
            var getSupId = con.PurchaseOrders.Where(p => p.Poid == POId)?.Select(s => s.SupId)?.FirstOrDefault() ?? 0;
            return getSupId;
        }


        public double AvailableOutStockQty(int ItemId, int SiteId)
        {
            var Qty = 0.0;
            var InStock = con.InStocks.Where(it => it.ItemId == ItemId && it.SiteId == SiteId)?.ToList()?.Sum(q => q.Quantity) ?? 0.0;
            var OutStock = con.OutStocks.Where(it => it.ItemId == ItemId && it.SiteId == SiteId)?.ToList()?.Sum(q => q.Quantity) ?? 0.0;
            Qty = InStock - OutStock;
            return Qty;
        }


        //public async Task<PagedResponse<List<TEntity>>> SearchDataWithPaging(PaginatedInputModel model)
        //{
        //    #region [Filter]  
        //    if (model.FilterParam != null && model.FilterParam.Any())
        //    {
        //        var data = DataFilter<TEntity>.FilteredData(model.FilterParam, _entities);

        //        var count = data.Count();

        //        data = DataSorting<TEntity>.SortData(data, model.SortingParams);

        //        data = await PaginatedList<TEntity>.CreateAsync(data.ToList(), model.PageNumber, model.PageSize);

        //        var response = CreatePagedReponse<TEntity>(data.ToList(), count, model.PageNumber, model.PageSize);
        //        return response;
        //    }
        //    else
        //    {
        //        var count = _entities.Count();
        //        var data = await PaginatedList<TEntity>.CreateAsync(_entities.ToList(), model.PageNumber, model.PageSize);

        //        var response = CreatePagedReponse<TEntity>(data, count, model.PageNumber, model.PageSize);
        //        return response;
        //    }


        //    #endregion
        //}
    }
}