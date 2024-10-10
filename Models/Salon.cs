using System.Text.Json.Serialization;

namespace Models
{
    public class Salon
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Employee
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SalonId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Master
    {
        public int Id { get; set; }
        public int SalonId { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool IsTerminated { get; set; }
        public DateTime CreatedAt { get; set; }

        [JsonIgnore]
        public Salary Salary { get; set; }
    }

    public class Procedure
    {
        public int Id { get; set; }
        public int SalonId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Income
    {
        public int Id { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public int SalonId { get; set; }
        public DateTime IncomeDate { get; set; }
        public string? Comment { get; set; }
        public Salon Salon { get; set; }
    }

    public class Expense
    {
        public int Id { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public int SalonId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? Comment { get; set; }
        
        public bool IsSalary { get; set; }
        public int? EmployeeId { get; set; }
        public bool DeductFromCash { get; set; }

        public Salon Salon { get; set; }
        public Employee? Employee { get; set; }
    }

    public class Odsetek
    {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public int ProcedureId { get; set; }
        public decimal Percentage { get; set; }

        public Master Master { get; set; }
        public Procedure Procedure { get; set; }
    }

    public class Salary
    {
        public int? MasterId { get; set; }
        public decimal Balance { get; set; }

        public Master Master { get; set; }
    }

    public class OperationHistory
    {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public decimal Amount { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public DateTime OperationDate { get; set; }
        public bool IsCanceled { get; set; }

        public Master Master { get; set; } = null!;
    }
}
