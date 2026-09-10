using SkolasCelvedis.JsonHandle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static SkolasCelvedis.Models;

namespace SkolasCelvedis
{
    public partial class ViewEmployees : Window
    {
        private readonly JsonRepository<Employee> _employees;
        private readonly JsonRepository<EmployeeRoom> _employeeRooms;
        private readonly JsonRepository<Room> _rooms;
        private readonly JsonRepository<Subject> _subjects;
        private readonly JsonRepository<EmployeeSubject> _employeeSubjects;

        public class EmployeeRow
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string RoomName { get; set; }
            public string SubjectName { get; set; }
        }
        public ViewEmployees(
            JsonRepository<Employee> employees,
            JsonRepository<EmployeeRoom> employeeRooms,
            JsonRepository<Room> rooms,
            JsonRepository<Subject> subjects,
            JsonRepository<EmployeeSubject> employeeSubjects,
            bool isAdmin = false)
        {
            InitializeComponent();
            _employees = employees;
            _employeeRooms = employeeRooms;
            _rooms = rooms;
            _subjects = subjects;
            _employeeSubjects = employeeSubjects;

            AddNewButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            EditButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            var allEmployees = _employees.GetAll();
            var allRooms = _rooms.GetAll();
            var allSubjects = _subjects.GetAll();
            var allEmployeeRooms = _employeeRooms.GetAll();
            var allEmployeeSubjects = _employeeSubjects.GetAll();

            var rows = allEmployees.Select(emp =>
            {
                var empRoom = allEmployeeRooms.FirstOrDefault(er => er.EmployeeId == emp.Id);
                var room = empRoom != null ? allRooms.FirstOrDefault(r => r.Id == empRoom.RoomId) : null;

                var empSubject = allEmployeeSubjects.FirstOrDefault(es => es.EmployeeId == emp.Id);
                var subject = empSubject != null ? allSubjects.FirstOrDefault(s => s.Id == empSubject.SubjectId) : null;

                return new EmployeeRow
                {
                    Id = emp.Id,
                    FirstName = emp.First_Name,
                    LastName = emp.Last_Name,
                    RoomName = room?.Name ?? "Nav piešķirts",
                    SubjectName = subject?.name ?? "Nav piešķirts"
                };
            }).ToList();

            EmployeeDataGrid.ItemsSource = rows;
            EditButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
        }

        private void EmployeeDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = EmployeeDataGrid.SelectedItem != null;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
        }

        private void AddNewButton_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddEmployee(_employees);
            addWindow.ShowDialog();
            RefreshGrid();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = EmployeeDataGrid.SelectedItem as EmployeeRow;
            if (selected == null) return;

            var allEmployees = _employees.GetAll();
            var emp = allEmployees.FirstOrDefault(e2 => e2.Id == selected.Id);
            if (emp == null) return;

            var editWindow = new AddEmployee(_employees, emp);
            editWindow.ShowDialog();
            RefreshGrid();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = EmployeeDataGrid.SelectedItem as EmployeeRow;
            if (selected == null) return;

            var confirm = MessageBox.Show(
                $"Vai tiešām dzēst šo ierakstu?",
                "Dzēst darbinieku",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var allEmployees = _employees.GetAll();
            allEmployees.RemoveAll(emp => emp.Id == selected.Id);
            _employees.SaveAll(allEmployees);

            var allEmpRooms = _employeeRooms.GetAll();
            allEmpRooms.RemoveAll(er => er.EmployeeId == selected.Id);
            _employeeRooms.SaveAll(allEmpRooms);

            var allEmployeeSubjects = _employeeSubjects.GetAll();
            allEmployeeSubjects.RemoveAll(es => es.EmployeeId == selected.Id);
            _employeeSubjects.SaveAll(allEmployeeSubjects);

            RefreshGrid();
        }
    }
}
