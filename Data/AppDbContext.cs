using Microsoft.EntityFrameworkCore;
using Models;

namespace Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Salon> Salons { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Master> Masters { get; set; }
        public DbSet<Procedure> Procedures { get; set; }
        public DbSet<UnlockCode> UnlockCodes { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Income> Income { get; set; }
        public DbSet<Expense> Expense { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
        public DbSet<Odsetek> Odsetek { get; set; }
        public DbSet<Salary> Salary { get; set; }
        public DbSet<OperationHistory> OperationHistory { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {   
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.Id)
                .HasColumnName("id");

            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.Login)
                .HasColumnName("login");

            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.PasswordHash)
                .HasColumnName("password_hash");

            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.CreatedAt)
                .HasColumnName("created_at");

            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.UserType)
                .HasColumnName("user_type");

            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.AccessToken)
                .HasColumnName("access_token");

            modelBuilder.Entity<User>()
                .ToTable("users")
                .Property(u => u.RefreshToken)
                .HasColumnName("refresh_token");

            modelBuilder.Entity<Salon>()
                .ToTable("salons")
                .Property(s => s.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Salon>()
                .ToTable("salons")
                .Property(s => s.Name)
                .HasColumnName("name");

            modelBuilder.Entity<Salon>()
                .ToTable("salons")
                .Property(s => s.PasswordHash)
                .HasColumnName("password_hash");

            modelBuilder.Entity<Salon>()
                .ToTable("salons")
                .Property(s => s.CreatedAt)
                .HasColumnName("created_at");
            
            modelBuilder.Entity<Employee>()
                .ToTable("employees")
                .Property(e => e.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Employee>()
                .ToTable("employees")
                .Property(e => e.UserId)
                .HasColumnName("user_id");

            modelBuilder.Entity<Employee>()
                .ToTable("employees")
                .Property(e => e.SalonId)
                .HasColumnName("salon_id");

            modelBuilder.Entity<Employee>()
                .ToTable("employees")
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            modelBuilder.Entity<Master>()
                .ToTable("masters")
                .Property(m => m.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Master>()
                .ToTable("masters")
                .Property(m => m.SalonId)
                .HasColumnName("salon_id");

            modelBuilder.Entity<Master>()
                .ToTable("masters")
                .Property(m => m.IsTerminated)
                .HasColumnName("is_terminated");

            modelBuilder.Entity<Master>()
                .ToTable("masters")
                .Property(m => m.Password)
                .HasColumnName("password");

            modelBuilder.Entity<Master>()
                .ToTable("masters")
                .Property(m => m.Name)
                .HasColumnName("name");

            modelBuilder.Entity<Master>()
                .ToTable("masters")
                .Property(m => m.CreatedAt)
                .HasColumnName("created_at");

            modelBuilder.Entity<Procedure>()
                .ToTable("procedures")
                .Property(p => p.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Procedure>()
                .ToTable("procedures")
                .Property(p => p.SalonId)
                .HasColumnName("salon_id");

            modelBuilder.Entity<Procedure>()
                .ToTable("procedures")
                .Property(p => p.Name)
                .HasColumnName("name");

            modelBuilder.Entity<Procedure>()
                .ToTable("procedures")
                .Property(p => p.CreatedAt)
                .HasColumnName("created_at");

            modelBuilder.Entity<UnlockCode>()
                .ToTable("unlock_codes")
                .Property(uc => uc.Id)
                .HasColumnName("id");

            modelBuilder.Entity<UnlockCode>()
                .ToTable("unlock_codes")
                .Property(uc => uc.Code)
                .HasColumnName("code");

            modelBuilder.Entity<UnlockCode>()
                .ToTable("unlock_codes")
                .Property(uc => uc.BlockType)
                .HasColumnName("block_type");

            modelBuilder.Entity<UnlockCode>()
                .ToTable("unlock_codes")
                .Property(uc => uc.UserId)
                .HasColumnName("user_id");

            modelBuilder.Entity<UnlockCode>()
                .ToTable("unlock_codes")
                .Property(uc => uc.QrCode)
                .HasColumnName("qr_code");
            
            modelBuilder.Entity<UnlockCode>()
                .ToTable("unlock_codes")
                .Property(uc => uc.SalonId)
                .HasColumnName("salon_id");
            
            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.ClientName)
                .HasColumnName("client_name");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.MasterId)
                .HasColumnName("master_id");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.ProcedureId)
                .HasColumnName("procedure_id");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.CashAmount)
                .HasColumnName("cash_amount");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.CardAmount)
                .HasColumnName("card_amount");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.FakeAmount)
                .HasColumnName("fake_amount");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.GrouponAmount)
                .HasColumnName("groupon_amount");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.SalonId)
                .HasColumnName("salon_id");

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.AppointmentDate)
                .HasColumnName("appointment_date");

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Master)
                .WithMany()
                .HasForeignKey(a => a.MasterId);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Procedure)
                .WithMany()
                .HasForeignKey(a => a.ProcedureId);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Salon)
                .WithMany()
                .HasForeignKey(a => a.SalonId);

            modelBuilder.Entity<Appointment>()
                .ToTable("appointments")
                .Property(a => a.Comment)
                .HasColumnName("comment");

            modelBuilder.Entity<Income>()
                .ToTable("income")
                .Property(i => i.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Income>()
                .ToTable("income")
                .Property(i => i.CashAmount)
                .HasColumnName("cash_amount");

            modelBuilder.Entity<Income>()
                .ToTable("income")
                .Property(i => i.CardAmount)
                .HasColumnName("card_amount");

            modelBuilder.Entity<Income>()
                .ToTable("income")
                .Property(i => i.SalonId)
                .HasColumnName("salon_id");

            modelBuilder.Entity<Income>()
                .ToTable("income")
                .Property(i => i.IncomeDate)
                .HasColumnName("income_date");
            
            modelBuilder.Entity<Income>()
                .ToTable("income")
                .Property(i => i.Comment)
                .HasColumnName("comment");

            modelBuilder.Entity<Income>()
                .HasOne(i => i.Salon)
                .WithMany()
                .HasForeignKey(i => i.SalonId);

            modelBuilder.Entity<Expense>()
                .ToTable("expenses")
                .Property(e => e.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Expense>()
                .Property(e => e.CashAmount)
                .HasColumnName("cash_amount");

            modelBuilder.Entity<Expense>()
                .Property(e => e.CardAmount)
                .HasColumnName("card_amount");

            modelBuilder.Entity<Expense>()
                .Property(e => e.SalonId)
                .HasColumnName("salon_id");

            modelBuilder.Entity<Expense>()
                .Property(e => e.ExpenseDate)
                .HasColumnName("expense_date");

            modelBuilder.Entity<Expense>()
                .Property(e => e.Comment)
                .HasColumnName("comment");

            modelBuilder.Entity<Expense>()
                .Property(e => e.IsSalary)
                .HasColumnName("is_salary");

            modelBuilder.Entity<Expense>()
                .Property(e => e.EmployeeId)
                .HasColumnName("employee_id");

            modelBuilder.Entity<Expense>()
                .Property(e => e.DeductFromCash)
                .HasColumnName("deduct_from_cash");

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Salon)
                .WithMany()
                .HasForeignKey(e => e.SalonId);
            
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Master)
                .WithMany()
                .HasForeignKey(a => a.MasterId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Salon)
                .WithMany()
                .HasForeignKey(a => a.SalonId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ErrorLog>()
                .ToTable("error_logs")
                .Property(e => e.Id)
                .HasColumnName("id");

            modelBuilder.Entity<ErrorLog>()
                .ToTable("error_logs")
                .Property(e => e.Message)
                .HasColumnName("message");

            modelBuilder.Entity<ErrorLog>()
                .ToTable("error_logs")
                .Property(e => e.Exception)
                .HasColumnName("exception");

            modelBuilder.Entity<ErrorLog>()
                .ToTable("error_logs")
                .Property(e => e.Timestamp)
                .HasColumnName("timestamp");

            modelBuilder.Entity<Odsetek>()
                .ToTable("odsetek")
                .Property(o => o.Id)
                .HasColumnName("id");

            modelBuilder.Entity<Odsetek>()
                .ToTable("odsetek")
                .Property(o => o.MasterId)
                .HasColumnName("master_id");

            modelBuilder.Entity<Odsetek>()
                .ToTable("odsetek")
                .Property(o => o.ProcedureId)
                .HasColumnName("procedure_id");

            modelBuilder.Entity<Odsetek>()
                .ToTable("odsetek")
                .Property(o => o.Percentage)
                .HasColumnName("percentage");
            
            modelBuilder.Entity<Salary>()
                .ToTable("salaries")
                .HasKey(s => s.MasterId);

            modelBuilder.Entity<Salary>()
                .Property(s => s.MasterId)
                .HasColumnName("master_id");

            modelBuilder.Entity<Salary>()
                .Property(s => s.Balance)
                .HasColumnName("balance")
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<Salary>()
                .HasOne(s => s.Master)
                .WithOne(m => m.Salary)
                .HasForeignKey<Salary>(s => s.MasterId);
            
            modelBuilder.Entity<OperationHistory>()
                .ToTable("operation_history");

            modelBuilder.Entity<OperationHistory>()
                .Property(oh => oh.Id)
                .HasColumnName("id");

            modelBuilder.Entity<OperationHistory>()
                .Property(oh => oh.MasterId)
                .HasColumnName("master_id");

            modelBuilder.Entity<OperationHistory>()
                .Property(oh => oh.IsCanceled)
                .HasColumnName("is_canceled");

            modelBuilder.Entity<OperationHistory>()
                .Property(oh => oh.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<OperationHistory>()
                .Property(oh => oh.OperationType)
                .HasColumnName("operation_type")
                .HasMaxLength(50);

            modelBuilder.Entity<OperationHistory>()
                .Property(oh => oh.OperationDate)
                .HasColumnName("operation_date")
                .HasColumnType("timestamp without time zone");

            // modelBuilder.Entity<OperationHistory>()
            //     .HasOne(oh => oh.Master)
            //     .WithMany(m => m.OperationHistories)
            //     .HasForeignKey(oh => oh.MasterId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
