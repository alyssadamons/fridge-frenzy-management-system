using E_Commerce.Areas.Dashboard.Models;

namespace E_Commerce.Dashboard.Services
{
    public interface IEmployeeService
    {
        (string email, string password) GenerateCredentials(string firstName, string lastName, string position);
        string GenerateEmail(string firstName, string lastName);
        string GeneratePassword(string firstName, string lastName, string position);
    }

    public class EmployeeService : IEmployeeService
    {
        public (string email, string password) GenerateCredentials(string firstName, string lastName, string position)
        {
            var email = GenerateEmail(firstName, lastName);
            var password = GeneratePassword(firstName, lastName, position);
            return (email, password);
        }

        public string GenerateEmail(string firstName, string lastName)
        {
            // Remove spaces and convert to lowercase
            var cleanFirstName = firstName.ToLower().Replace(" ", "").Replace("'", "");
            var cleanLastName = lastName.ToLower().Replace(" ", "").Replace("'", "");

            return $"{cleanFirstName}.{cleanLastName}@fridgefrenzy.com";
        }

        public string GeneratePassword(string firstName, string lastName, string position)
        {
            var firstInitial = char.ToUpper(firstName[0]);
            var lastInitial = char.ToUpper(lastName[0]);

            return position.ToLower() switch
            {
                "technician" => $"{firstInitial}{lastInitial}tech2023!",
                "sales" => $"{firstInitial}{lastInitial}_sales2023!",
                "customermanager" or "customer manager" => $"{firstInitial}{lastInitial}cm2023!",
                _ => $"{firstInitial}{lastInitial}emp2023!"
            };
        }
    }
}