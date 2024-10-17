using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PAMAPIs.Models;

namespace PAMAPIs.Data;

public partial class PAMContext : DbContext
{
    public PAMContext()
    {
    }

    public PAMContext(DbContextOptions<PAMContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<City> Cities { get; set; }

    public virtual DbSet<CostCode> CostCodes { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<CountryCurrency> CountryCurrencies { get; set; }

    public virtual DbSet<EmailSendLog> EmailSendLogs { get; set; }

    public virtual DbSet<InStock> InStocks { get; set; }

    public virtual DbSet<InWarehouse> InWarehouses { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<KpiFile> KpiFiles { get; set; }

    public virtual DbSet<MaterialDetail> MaterialDetails { get; set; }

    public virtual DbSet<MaterialRequest> MaterialRequests { get; set; }

    public virtual DbSet<MaterialTemp> MaterialTemps { get; set; }

    public virtual DbSet<OutStock> OutStocks { get; set; }

    public virtual DbSet<OutWarehouse> OutWarehouses { get; set; }

    public virtual DbSet<PayOrder> PayOrders { get; set; }

    public virtual DbSet<PayOrderDetail> PayOrderDetails { get; set; }

    public virtual DbSet<PayOrderTemp> PayOrderTemps { get; set; }

    public virtual DbSet<PoDetail> PoDetails { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<QrtzBlobTrigger> QrtzBlobTriggers { get; set; }

    public virtual DbSet<QrtzCalendar> QrtzCalendars { get; set; }

    public virtual DbSet<QrtzCronTrigger> QrtzCronTriggers { get; set; }

    public virtual DbSet<QrtzFiredTrigger> QrtzFiredTriggers { get; set; }

    public virtual DbSet<QrtzJobDetail> QrtzJobDetails { get; set; }

    public virtual DbSet<QrtzLock> QrtzLocks { get; set; }

    public virtual DbSet<QrtzPausedTriggerGrp> QrtzPausedTriggerGrps { get; set; }

    public virtual DbSet<QrtzSchedulerState> QrtzSchedulerStates { get; set; }

    public virtual DbSet<QrtzSimpleTrigger> QrtzSimpleTriggers { get; set; }

    public virtual DbSet<QrtzSimpropTrigger> QrtzSimpropTriggers { get; set; }

    public virtual DbSet<QrtzTrigger> QrtzTriggers { get; set; }

    public virtual DbSet<ReturnPurchaseOrder> ReturnPurchaseOrders { get; set; }

    public virtual DbSet<ReturnPurchaseOrderDetail> ReturnPurchaseOrderDetails { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Site> Sites { get; set; }

    public virtual DbSet<StockQuantity> StockQuantities { get; set; }

    public virtual DbSet<SubContractNumber> SubContractNumbers { get; set; }

    public virtual DbSet<SubContractor> SubContractors { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplierItem> SupplierItems { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserCountry> UserCountries { get; set; }

    public virtual DbSet<UserSite> UserSites { get; set; }

    public virtual DbSet<Vat> Vats { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<WarehouseQuantity> WarehouseQuantities { get; set; }

    public virtual DbSet<WarehouseTemp> WarehouseTemps { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=tcp:kaentvm.westeurope.cloudapp.azure.com,2356;Database=PAMdbTEST;UID=PAMdbUsr;PWD=hfhhjkt61223;MultipleActiveResultSets=True;Encrypt=False;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");

            entity.Property(e => e.CategoryName).HasMaxLength(50);
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.ToTable("City");

            entity.Property(e => e.CityName).HasMaxLength(150);
        });

        modelBuilder.Entity<CostCode>(entity =>
        {
            entity.HasKey(e => e.CodeId);

            entity.ToTable("Cost_Codes");

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.En)
                .HasMaxLength(150)
                .HasColumnName("EN");
            entity.Property(e => e.Fr)
                .HasMaxLength(150)
                .HasColumnName("FR");
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Country");

            entity.Property(e => e.CountryCode).HasMaxLength(30);
            entity.Property(e => e.CountryIcon)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CountryName).HasMaxLength(80);
        });

        modelBuilder.Entity<CountryCurrency>(entity =>
        {
            entity.HasKey(e => e.CurrencyId).HasName("PK_Currency");

            entity.ToTable("CountryCurrency");

            entity.Property(e => e.Currency).HasMaxLength(150);
        });

        modelBuilder.Entity<EmailSendLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailSen__3214EC0754D3C044");

            entity.Property(e => e.EmailType).HasMaxLength(100);
            entity.Property(e => e.SentAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<InStock>(entity =>
        {
            entity.HasKey(e => e.InId);

            entity.ToTable("InStock");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailId");
            entity.Property(e => e.Poid).HasColumnName("POId");
            entity.Property(e => e.RefNo)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<InWarehouse>(entity =>
        {
            entity.HasKey(e => e.InWareId).HasName("PK_InWarehouse_1");

            entity.ToTable("InWarehouse");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailId");
            entity.Property(e => e.Poid).HasColumnName("POId");
            entity.Property(e => e.RefNo)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.Property(e => e.ItemUnit).HasMaxLength(10);
        });

        modelBuilder.Entity<KpiFile>(entity =>
        {
            entity.ToTable("KPI_File");

            entity.Property(e => e.Kpifile1)
                .IsUnicode(false)
                .HasColumnName("KPIFile");
        });

        modelBuilder.Entity<MaterialDetail>(entity =>
        {
            entity.ToTable("Material_Detail");
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasKey(e => e.MaterialId);

            entity.ToTable("Material_Request");

            entity.Property(e => e.IsApprovedByPm).HasColumnName("IsApprovedByPM");
            entity.Property(e => e.RefNo)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MaterialTemp>(entity =>
        {
            entity.ToTable("Material_Temp");
        });

        modelBuilder.Entity<OutStock>(entity =>
        {
            entity.HasKey(e => e.OutId);

            entity.ToTable("OutStock");

            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.RefNo)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<OutWarehouse>(entity =>
        {
            entity.HasKey(e => e.OutWareId);

            entity.ToTable("OutWarehouse");

            entity.Property(e => e.RefNo)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PayOrder>(entity =>
        {
            entity.ToTable("PayOrder");

            entity.Property(e => e.PayOrderNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Poid).HasColumnName("POId");
            entity.Property(e => e.SupplierInvoiceNum).HasMaxLength(50);
        });

        modelBuilder.Entity<PayOrderDetail>(entity =>
        {
            entity.ToTable("PayOrderDetail");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailId");
        });

        modelBuilder.Entity<PayOrderTemp>(entity =>
        {
            entity.HasKey(e => e.PayOrderTempId).HasName("PK_TempPayOrder");

            entity.ToTable("PayOrderTemp");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailId");
            entity.Property(e => e.Poid).HasColumnName("POId");
        });

        modelBuilder.Entity<PoDetail>(entity =>
        {
            entity.ToTable("PO_Detail");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailId");
            entity.Property(e => e.IsVatbilled).HasColumnName("isVATBilled");
            entity.Property(e => e.Poid).HasColumnName("POId");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Poid);

            entity.ToTable("Purchase_Order");

            entity.Property(e => e.Poid).HasColumnName("POId");
            entity.Property(e => e.Currency).HasMaxLength(50);
            entity.Property(e => e.IsReturned).HasColumnName("isReturned");
            entity.Property(e => e.IsVatdiffere).HasColumnName("isVATDiffere");
            entity.Property(e => e.IsVatsuspended).HasColumnName("isVATSuspended");
            entity.Property(e => e.IsVatunbilled).HasColumnName("isVATUnbilled");
            entity.Property(e => e.Ponumber)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("PONumber");
            entity.Property(e => e.Postatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("POStatus");
            entity.Property(e => e.Vat).HasColumnName("VAT");
        });

        modelBuilder.Entity<QrtzBlobTrigger>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.TriggerName, e.TriggerGroup });

            entity.ToTable("QRTZ_BLOB_TRIGGERS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.TriggerName)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_NAME");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
            entity.Property(e => e.BlobData).HasColumnName("BLOB_DATA");
        });

        modelBuilder.Entity<QrtzCalendar>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.CalendarName });

            entity.ToTable("QRTZ_CALENDARS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.CalendarName)
                .HasMaxLength(200)
                .HasColumnName("CALENDAR_NAME");
            entity.Property(e => e.Calendar).HasColumnName("CALENDAR");
        });

        modelBuilder.Entity<QrtzCronTrigger>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.TriggerName, e.TriggerGroup });

            entity.ToTable("QRTZ_CRON_TRIGGERS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.TriggerName)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_NAME");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
            entity.Property(e => e.CronExpression)
                .HasMaxLength(120)
                .HasColumnName("CRON_EXPRESSION");
            entity.Property(e => e.TimeZoneId)
                .HasMaxLength(80)
                .HasColumnName("TIME_ZONE_ID");

            entity.HasOne(d => d.QrtzTrigger).WithOne(p => p.QrtzCronTrigger)
                .HasForeignKey<QrtzCronTrigger>(d => new { d.SchedName, d.TriggerName, d.TriggerGroup })
                .HasConstraintName("FK_QRTZ_CRON_TRIGGERS_QRTZ_TRIGGERS");
        });

        modelBuilder.Entity<QrtzFiredTrigger>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.EntryId });

            entity.ToTable("QRTZ_FIRED_TRIGGERS");

            entity.HasIndex(e => new { e.SchedName, e.JobGroup, e.JobName }, "IDX_QRTZ_FT_G_J");

            entity.HasIndex(e => new { e.SchedName, e.TriggerGroup, e.TriggerName }, "IDX_QRTZ_FT_G_T");

            entity.HasIndex(e => new { e.SchedName, e.InstanceName, e.RequestsRecovery }, "IDX_QRTZ_FT_INST_JOB_REQ_RCVRY");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.EntryId)
                .HasMaxLength(140)
                .HasColumnName("ENTRY_ID");
            entity.Property(e => e.FiredTime).HasColumnName("FIRED_TIME");
            entity.Property(e => e.InstanceName)
                .HasMaxLength(200)
                .HasColumnName("INSTANCE_NAME");
            entity.Property(e => e.IsNonconcurrent).HasColumnName("IS_NONCONCURRENT");
            entity.Property(e => e.JobGroup)
                .HasMaxLength(150)
                .HasColumnName("JOB_GROUP");
            entity.Property(e => e.JobName)
                .HasMaxLength(150)
                .HasColumnName("JOB_NAME");
            entity.Property(e => e.Priority).HasColumnName("PRIORITY");
            entity.Property(e => e.RequestsRecovery).HasColumnName("REQUESTS_RECOVERY");
            entity.Property(e => e.SchedTime).HasColumnName("SCHED_TIME");
            entity.Property(e => e.State)
                .HasMaxLength(16)
                .HasColumnName("STATE");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
            entity.Property(e => e.TriggerName)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_NAME");
        });

        modelBuilder.Entity<QrtzJobDetail>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.JobName, e.JobGroup });

            entity.ToTable("QRTZ_JOB_DETAILS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.JobName)
                .HasMaxLength(150)
                .HasColumnName("JOB_NAME");
            entity.Property(e => e.JobGroup)
                .HasMaxLength(150)
                .HasColumnName("JOB_GROUP");
            entity.Property(e => e.Description)
                .HasMaxLength(250)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.IsDurable).HasColumnName("IS_DURABLE");
            entity.Property(e => e.IsNonconcurrent).HasColumnName("IS_NONCONCURRENT");
            entity.Property(e => e.IsUpdateData).HasColumnName("IS_UPDATE_DATA");
            entity.Property(e => e.JobClassName)
                .HasMaxLength(250)
                .HasColumnName("JOB_CLASS_NAME");
            entity.Property(e => e.JobData).HasColumnName("JOB_DATA");
            entity.Property(e => e.RequestsRecovery).HasColumnName("REQUESTS_RECOVERY");
        });

        modelBuilder.Entity<QrtzLock>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.LockName });

            entity.ToTable("QRTZ_LOCKS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.LockName)
                .HasMaxLength(40)
                .HasColumnName("LOCK_NAME");
        });

        modelBuilder.Entity<QrtzPausedTriggerGrp>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.TriggerGroup });

            entity.ToTable("QRTZ_PAUSED_TRIGGER_GRPS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
        });

        modelBuilder.Entity<QrtzSchedulerState>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.InstanceName });

            entity.ToTable("QRTZ_SCHEDULER_STATE");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.InstanceName)
                .HasMaxLength(200)
                .HasColumnName("INSTANCE_NAME");
            entity.Property(e => e.CheckinInterval).HasColumnName("CHECKIN_INTERVAL");
            entity.Property(e => e.LastCheckinTime).HasColumnName("LAST_CHECKIN_TIME");
        });

        modelBuilder.Entity<QrtzSimpleTrigger>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.TriggerName, e.TriggerGroup });

            entity.ToTable("QRTZ_SIMPLE_TRIGGERS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.TriggerName)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_NAME");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
            entity.Property(e => e.RepeatCount).HasColumnName("REPEAT_COUNT");
            entity.Property(e => e.RepeatInterval).HasColumnName("REPEAT_INTERVAL");
            entity.Property(e => e.TimesTriggered).HasColumnName("TIMES_TRIGGERED");

            entity.HasOne(d => d.QrtzTrigger).WithOne(p => p.QrtzSimpleTrigger)
                .HasForeignKey<QrtzSimpleTrigger>(d => new { d.SchedName, d.TriggerName, d.TriggerGroup })
                .HasConstraintName("FK_QRTZ_SIMPLE_TRIGGERS_QRTZ_TRIGGERS");
        });

        modelBuilder.Entity<QrtzSimpropTrigger>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.TriggerName, e.TriggerGroup });

            entity.ToTable("QRTZ_SIMPROP_TRIGGERS");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.TriggerName)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_NAME");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
            entity.Property(e => e.BoolProp1).HasColumnName("BOOL_PROP_1");
            entity.Property(e => e.BoolProp2).HasColumnName("BOOL_PROP_2");
            entity.Property(e => e.DecProp1)
                .HasColumnType("numeric(13, 4)")
                .HasColumnName("DEC_PROP_1");
            entity.Property(e => e.DecProp2)
                .HasColumnType("numeric(13, 4)")
                .HasColumnName("DEC_PROP_2");
            entity.Property(e => e.IntProp1).HasColumnName("INT_PROP_1");
            entity.Property(e => e.IntProp2).HasColumnName("INT_PROP_2");
            entity.Property(e => e.LongProp1).HasColumnName("LONG_PROP_1");
            entity.Property(e => e.LongProp2).HasColumnName("LONG_PROP_2");
            entity.Property(e => e.StrProp1)
                .HasMaxLength(512)
                .HasColumnName("STR_PROP_1");
            entity.Property(e => e.StrProp2)
                .HasMaxLength(512)
                .HasColumnName("STR_PROP_2");
            entity.Property(e => e.StrProp3)
                .HasMaxLength(512)
                .HasColumnName("STR_PROP_3");
            entity.Property(e => e.TimeZoneId)
                .HasMaxLength(80)
                .HasColumnName("TIME_ZONE_ID");

            entity.HasOne(d => d.QrtzTrigger).WithOne(p => p.QrtzSimpropTrigger)
                .HasForeignKey<QrtzSimpropTrigger>(d => new { d.SchedName, d.TriggerName, d.TriggerGroup })
                .HasConstraintName("FK_QRTZ_SIMPROP_TRIGGERS_QRTZ_TRIGGERS");
        });

        modelBuilder.Entity<QrtzTrigger>(entity =>
        {
            entity.HasKey(e => new { e.SchedName, e.TriggerName, e.TriggerGroup });

            entity.ToTable("QRTZ_TRIGGERS");

            entity.HasIndex(e => new { e.SchedName, e.CalendarName }, "IDX_QRTZ_T_C");

            entity.HasIndex(e => new { e.SchedName, e.JobGroup, e.JobName }, "IDX_QRTZ_T_G_J");

            entity.HasIndex(e => new { e.SchedName, e.NextFireTime }, "IDX_QRTZ_T_NEXT_FIRE_TIME");

            entity.HasIndex(e => new { e.SchedName, e.TriggerState, e.NextFireTime }, "IDX_QRTZ_T_NFT_ST");

            entity.HasIndex(e => new { e.SchedName, e.MisfireInstr, e.NextFireTime, e.TriggerState }, "IDX_QRTZ_T_NFT_ST_MISFIRE");

            entity.HasIndex(e => new { e.SchedName, e.MisfireInstr, e.NextFireTime, e.TriggerGroup, e.TriggerState }, "IDX_QRTZ_T_NFT_ST_MISFIRE_GRP");

            entity.HasIndex(e => new { e.SchedName, e.TriggerGroup, e.TriggerState }, "IDX_QRTZ_T_N_G_STATE");

            entity.HasIndex(e => new { e.SchedName, e.TriggerName, e.TriggerGroup, e.TriggerState }, "IDX_QRTZ_T_N_STATE");

            entity.HasIndex(e => new { e.SchedName, e.TriggerState }, "IDX_QRTZ_T_STATE");

            entity.Property(e => e.SchedName)
                .HasMaxLength(120)
                .HasColumnName("SCHED_NAME");
            entity.Property(e => e.TriggerName)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_NAME");
            entity.Property(e => e.TriggerGroup)
                .HasMaxLength(150)
                .HasColumnName("TRIGGER_GROUP");
            entity.Property(e => e.CalendarName)
                .HasMaxLength(200)
                .HasColumnName("CALENDAR_NAME");
            entity.Property(e => e.Description)
                .HasMaxLength(250)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.EndTime).HasColumnName("END_TIME");
            entity.Property(e => e.JobData).HasColumnName("JOB_DATA");
            entity.Property(e => e.JobGroup)
                .HasMaxLength(150)
                .HasColumnName("JOB_GROUP");
            entity.Property(e => e.JobName)
                .HasMaxLength(150)
                .HasColumnName("JOB_NAME");
            entity.Property(e => e.MisfireInstr).HasColumnName("MISFIRE_INSTR");
            entity.Property(e => e.NextFireTime).HasColumnName("NEXT_FIRE_TIME");
            entity.Property(e => e.PrevFireTime).HasColumnName("PREV_FIRE_TIME");
            entity.Property(e => e.Priority).HasColumnName("PRIORITY");
            entity.Property(e => e.StartTime).HasColumnName("START_TIME");
            entity.Property(e => e.TriggerState)
                .HasMaxLength(16)
                .HasColumnName("TRIGGER_STATE");
            entity.Property(e => e.TriggerType)
                .HasMaxLength(8)
                .HasColumnName("TRIGGER_TYPE");

            entity.HasOne(d => d.QrtzJobDetail).WithMany(p => p.QrtzTriggers)
                .HasForeignKey(d => new { d.SchedName, d.JobName, d.JobGroup })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QRTZ_TRIGGERS_QRTZ_JOB_DETAILS");
        });

        modelBuilder.Entity<ReturnPurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Rpoid);

            entity.ToTable("ReturnPurchaseOrder");

            entity.Property(e => e.Rpoid).HasColumnName("RPOId");
            entity.Property(e => e.Poid).HasColumnName("POId");
            entity.Property(e => e.ReturnNo)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ReturnPurchaseOrderDetail>(entity =>
        {
            entity.HasKey(e => e.RpodetailId);

            entity.ToTable("ReturnPurchaseOrderDetail");

            entity.Property(e => e.RpodetailId).HasColumnName("RPODetailId");
            entity.Property(e => e.PodetailId).HasColumnName("PODetailId");
            entity.Property(e => e.Rpoid).HasColumnName("RPOId");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.Property(e => e.Acronym).HasMaxLength(30);
            entity.Property(e => e.CityName)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.SiteCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SiteName).HasMaxLength(150);
        });

        modelBuilder.Entity<StockQuantity>(entity =>
        {
            entity.HasKey(e => e.QtyId);

            entity.ToTable("StockQuantity");
        });

        modelBuilder.Entity<SubContractNumber>(entity =>
        {
            entity.HasKey(e => e.NumId);

            entity.ToTable("SubContractNumber");

            entity.Property(e => e.ContractNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Trade)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SubContractor>(entity =>
        {
            entity.HasKey(e => e.SubId);

            entity.Property(e => e.SubName).HasMaxLength(150);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupId);

            entity.Property(e => e.SupContactNo).HasMaxLength(50);
            entity.Property(e => e.SupEmail).HasMaxLength(50);
            entity.Property(e => e.SupFax).HasMaxLength(50);
            entity.Property(e => e.SupName).HasMaxLength(150);
            entity.Property(e => e.SupRepresentative).HasMaxLength(150);
        });

        modelBuilder.Entity<SupplierItem>(entity =>
        {
            entity.HasKey(e => e.SupItemId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UsrId);

            entity.Property(e => e.LastLogin).HasColumnType("datetime");
            entity.Property(e => e.UserCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserEmail)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.UserPassword)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserCountry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__userCoun__3214EC077DBF0BFE");

            entity.ToTable("userCountry");

            entity.HasOne(d => d.Country).WithMany(p => p.UserCountries)
                .HasForeignKey(d => d.CountryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__userCount__Count__4DB4832C");

            entity.HasOne(d => d.Usr).WithMany(p => p.UserCountries)
                .HasForeignKey(d => d.UsrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__userCount__UsrId__4CC05EF3");
        });

        modelBuilder.Entity<UserSite>(entity =>
        {
            entity.HasKey(e => e.UsId);

            entity.Property(e => e.UsId).HasColumnName("usId");
        });

        modelBuilder.Entity<Vat>(entity =>
        {
            entity.ToTable("VAT");

            entity.Property(e => e.Vat1).HasColumnName("VAT");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouse");
        });

        modelBuilder.Entity<WarehouseQuantity>(entity =>
        {
            entity.HasKey(e => e.Wqty);

            entity.ToTable("WarehouseQuantity");

            entity.Property(e => e.Wqty).HasColumnName("WQty");
        });

        modelBuilder.Entity<WarehouseTemp>(entity =>
        {
            entity.ToTable("WarehouseTemp");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
