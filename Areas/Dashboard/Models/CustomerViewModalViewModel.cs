namespace E_Commerce.Areas.Dashboard.Models
{
    public class CustomerViewModalViewModel
    {
        public Customer Customer { get; set; } = new Customer();
        public List<AppointmentViewModel> UpcomingAppointments { get; set; } = new List<AppointmentViewModel>();
        public List<AppointmentViewModel> PastAppointments { get; set; } = new List<AppointmentViewModel>();
    }
}
