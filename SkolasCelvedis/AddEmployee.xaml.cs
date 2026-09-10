using SkolasCelvedis.JsonHandle;
using System.Linq;
using System.Windows;
using static SkolasCelvedis.Models;

namespace SkolasCelvedis
{
    public partial class AddEmployee : Window
    {
        private readonly JsonRepository<Employee> _employees;
        private Employee _editingEmployee = null;

        public AddEmployee(JsonRepository<Employee> employees)
        {
            InitializeComponent();
            _employees = employees;
        }

        public AddEmployee(JsonRepository<Employee> employees, Employee existing) : this(employees)
        {
            _editingEmployee = existing;
            NameTextBox.Text = existing.First_Name;
            LastNameTextBox.Text = existing.Last_Name;
            Title = "Rediģēt darbinieku";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string firstName = NameTextBox.Text.Trim();
            string lastName = LastNameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            {
                MessageBox.Show("Lūdzu ievadiet vārdu un uzvārdu!");
                return;
            }
            if (firstName.Any(char.IsDigit) || lastName.Any(char.IsDigit))
            {
                MessageBox.Show("Vārds un uzvārds nedrīkst saturēt ciparus!");
                return;
            }

            if (!firstName.All(c => char.IsLetter(c) || c == '-' || c == ' ') ||
                !lastName.All(c => char.IsLetter(c) || c == '-' || c == ' '))
            {
                MessageBox.Show("Vārds un uzvārds drīkst saturēt tikai burtus!");
                return;
            }

            var all = _employees.GetAll();

            if (_editingEmployee != null)
            {
                bool duplicateEdit = all.Any(emp =>
                    emp.Id != _editingEmployee.Id &&
                    emp.First_Name.ToLower() == firstName.ToLower() &&
                    emp.Last_Name.ToLower() == lastName.ToLower());

                if (duplicateEdit)
                {
                    MessageBox.Show($"Darbinieks '{firstName} {lastName}' jau eksistē!");
                    return;
                }

                var target = all.FirstOrDefault(emp => emp.Id == _editingEmployee.Id);
                if (target != null)
                {
                    target.First_Name = firstName;
                    target.Last_Name = lastName;
                    _employees.SaveAll(all);
                    MessageBox.Show("Darbinieks atjaunināts!");
                }
            }
            else
            {
                bool duplicate = all.Any(emp =>
                    emp.First_Name.ToLower() == firstName.ToLower() &&
                    emp.Last_Name.ToLower() == lastName.ToLower());

                if (duplicate)
                {
                    MessageBox.Show($"Darbinieks '{firstName} {lastName}' jau eksistē!");
                    return;
                }

                var newEmployee = new Employee
                {
                    Id = all.Count > 0 ? all.Max(emp => emp.Id) + 1 : 1,
                    First_Name = firstName,
                    Last_Name = lastName
                };
                _employees.Add(newEmployee);
                MessageBox.Show("Darbinieks saglabāts!");
            }

            this.Close();
        }
    }
}
