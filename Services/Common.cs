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

        public string WarehouseInStock(int RoleId, int UserID, int SiteId, double Qty, int ItemId, int supId, int poId, int podId, int wareNo, string refNo, string SuppDeliveryNote)
        {
            try
            {
                //Generating Ref Number
                //var RefNo = "";
                //var WareNo = 0;
                //string SiteCode = con.sites.Where(s => s.SiteId == SiteId).Select(c => c.SiteCode).FirstOrDefault();
                //if (SiteCode != null)
                //{
                //    int GetNumber = con.inWarehouses.Where(s => s.SiteId == SiteId).OrderByDescending(o => o.InWareId).Select(m => m.WareNo).FirstOrDefault();
                //    if (GetNumber != 0)
                //    {
                //        int rNo = GetNumber + 1;

                //        RefNo = "BT-" + SiteCode + "-" + rNo.ToString("D4") + "";
                //        WareNo = rNo;
                //    }
                //    else
                //    {
                //        RefNo = "BT-" + SiteCode + "-0001";
                //        WareNo = 1;
                //    }
                //}

                InWarehouse warehouse = new()
                {
                    RefNo = refNo,
                    WareNo = wareNo,
                    Date = DateTime.Now,
                    Quantity = Qty,
                    ItemId = ItemId,
                    SiteId = SiteId,
                    UsrId = UserID,
                    SupId = supId,
                    Poid = poId,
                    PodetailId = podId,
                    SuppDeliveryNote = SuppDeliveryNote
                };
                con.InWarehouses.Add(warehouse);

                //Add Quantity
                WarehouseQuantity CheckItem = con.WarehouseQuantities.Where(q => q.ItemId == ItemId).FirstOrDefault();
                if (CheckItem != null)
                {
                    CheckItem.QtyReceived += Qty;
                    CheckItem.QtyStock += Qty;
                    con.Entry(CheckItem).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                }
                else
                {
                    WarehouseQuantity stock = new()
                    {
                        SiteId = 0,
                        UsrId = UserID,
                        ItemId = ItemId,
                        QtyReceived = Qty,
                        QtyStock = Qty
                    };
                    con.WarehouseQuantities.Add(stock);
                }
                con.SaveChanges();

            }
            catch (Exception)
            {
                return "NOT DONE";
            }
            return "DONE";
        }

        public int GetSupplierId(int POId)
        {
            var getSupId = con.PurchaseOrders.Where(p => p.Poid == POId)?.Select(s => s.SupId)?.FirstOrDefault() ?? 0;
            return getSupId;
        }

        public int WarehouseOperationCount(int SiteId, int SpecificSiteId, int CountryId, int SpecificCountryId)
        {
            int Count = 0;
            if (SiteId != 0)
            {
                var final = con.InWarehouses.DefaultIfEmpty().Select(t => new
                {
                    One = con.InWarehouses.Where(p => p.SiteId == SiteId).Count(),
                    Two = con.OutWarehouses.Where(p => p.SiteId == SiteId).Count()
                }).FirstOrDefault();
                Count = final.One + final.Two;
                return Count;
            }
            if (SpecificSiteId != 0)
            {
                var final = con.InWarehouses.DefaultIfEmpty().Select(t => new
                {
                    One = con.InWarehouses.Where(p => p.SiteId == SpecificSiteId).Count(),
                    Two = con.OutWarehouses.Where(p => p.SiteId == SpecificSiteId).Count()
                }).FirstOrDefault();
                Count = final.One + final.Two;
                return Count;
            }
            if (CountryId != 0)
            {
                var final = con.InWarehouses.DefaultIfEmpty().Select(t => new
                {
                    One = con.InWarehouses.Where(p => p.GetSites.CountryId == CountryId).Count(),
                    Two = con.OutWarehouses.Where(p => p.GetSites.CountryId == CountryId).Count()
                }).FirstOrDefault();
                Count = final.One + final.Two;
                return Count;
            }
            if (SpecificCountryId != 0)
            {
                var final = con.InWarehouses.DefaultIfEmpty().Select(t => new
                {
                    One = con.InWarehouses.Where(p => p.GetSites.CountryId == SpecificCountryId).Count(),
                    Two = con.OutWarehouses.Where(p => p.GetSites.CountryId == SpecificCountryId).Count()
                }).FirstOrDefault();
                Count = final.One + final.Two;
                return Count;
            }

            return 0;
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