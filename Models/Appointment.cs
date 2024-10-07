namespace Models {
    public class Appointment
    {
        public int Id { get; set; }
        public string ClientName { get; set; }
        public int? MasterId { get; set; }
        public int? ProcedureId { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public decimal FakeAmount { get; set; }
        public decimal GrouponAmount { get; set; }
        public int SalonId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string? Comment { get; set; }

        public virtual Master Master { get; set; }
        public virtual Procedure Procedure { get; set; }
        public virtual Salon Salon { get; set; }
    }
}
