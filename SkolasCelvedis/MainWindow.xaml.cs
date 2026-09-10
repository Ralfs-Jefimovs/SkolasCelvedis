using Newtonsoft.Json;
using SharpVectors.Converters;
using SharpVectors.Dom.Svg;
using SharpVectors.Renderers.Utils;
using SharpVectors.Renderers.Wpf;
using SharpVectors.Runtime;
using SkolasCelvedis.JsonHandle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static SkolasCelvedis.Models;

namespace SkolasCelvedis
{
    public partial class MainWindow : Window
    {
        //DataFolder located in Project/bin/Debug/DataFolder
        private string dataFolder = "DataFolder";

        private Point _lastMouse;
        private bool _isDragging = false;

        private bool _showingWaypoints = false;

        private List<GeometryDrawing> _allGeometries = new List<GeometryDrawing>();
        private Dictionary<string, GeometryDrawing> _roomsById = new Dictionary<string, GeometryDrawing>();

        private GeometryDrawing _highlightedGeometry = null;
        private Brush _originalBrush = null;

        private string _currentFloor = "first_floor";

        private Dictionary<string, List<string>> _navigationSegments = new Dictionary<string, List<string>>();

        private JsonRepository<Floor> floors;
        private JsonRepository<Room> rooms;
        private JsonRepository<Employee> employees;
        private JsonRepository<Subject> subjects;
        private JsonRepository<EmployeeRoom> employeeRooms;
        private JsonRepository<EmployeeSubject> employeeSubjects;
        private JsonRepository<FloorNavigation> navigation;

        private RoomSearchResult _selectedRoom = null;

        private double _svgRoomOffsetX = 0;
        private double _svgRoomOffsetY = 0;

        private bool _editingMode = false;

        private string password = "RTK2026";
        public MainWindow()
        {


            InitializeComponent();

            //Json Handling in main code
            string BasePath = AppDomain.CurrentDomain.BaseDirectory;
            dataFolder = System.IO.Path.Combine(BasePath, "JsonData");
            Directory.CreateDirectory(dataFolder);

            floors = new JsonRepository<Floor>(System.IO.Path.Combine(dataFolder, "floors.json"));
            rooms = new JsonRepository<Room>(System.IO.Path.Combine(dataFolder, "rooms.json"));
            employees = new JsonRepository<Employee>(System.IO.Path.Combine(dataFolder, "employees.json"));
            subjects = new JsonRepository<Subject>(System.IO.Path.Combine(dataFolder, "subjects.json"));
            employeeRooms = new JsonRepository<EmployeeRoom>(System.IO.Path.Combine(dataFolder, "employeeRooms.json"));
            employeeSubjects = new JsonRepository<EmployeeSubject>(System.IO.Path.Combine(dataFolder, "employeeSubjects.json"));
            navigation = new JsonRepository<FloorNavigation>(System.IO.Path.Combine(dataFolder, "navigation.json"));

            BuildingComboBox_SelectionChanged(null, null);

            SeedStaticData();

            Loaded += MainWindow_Loaded;
            Quit_Map();

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var drawing = FloorSvgCanvas.Drawings as DrawingGroup;
            ReloadSvgAndExtract();
            if (drawing != null)
            {
                _allGeometries.Clear();
                _roomsById.Clear();
                ExtractGeometries(drawing);
            }
        }

        public class SeedData
        {
            public List<Floor> Floors { get; set; }
            public List<Room> Rooms { get; set; }
            public List<Subject> Subjects { get; set; }
        }

        private void SeedStaticData()
        {
            string seedPath = System.IO.Path.Combine(
       AppDomain.CurrentDomain.BaseDirectory, "JsonHandle", "seed.json");

            if (!System.IO.File.Exists(seedPath))
            {
                MessageBox.Show("seed.json not found! \n\nLooked in:" + seedPath);
                return;
            }

            string json = System.IO.File.ReadAllText(seedPath);
            var seedData = JsonConvert.DeserializeObject<SeedData>(json);

            if (!floors.GetAll().Any())
            {
                if (seedData.Floors != null) floors.SaveAll(seedData.Floors);
                if (seedData.Rooms != null) rooms.SaveAll(seedData.Rooms);
            }

            if (!subjects.GetAll().Any())
            {
                if (seedData.Subjects != null) subjects.SaveAll(seedData.Subjects);
            }
        }

        private void Map_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point current = e.GetPosition(TransformWrapper);

            MapTranslateTransform.X += current.X - _lastMouse.X;
            MapTranslateTransform.Y += current.Y - _lastMouse.Y;

            _lastMouse = current;

            ClampTranslation();
        }

        private void Map_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMouse = e.GetPosition(TransformWrapper);
            TransformWrapper.CaptureMouse();
        }

        private void Map_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            TransformWrapper.ReleaseMouseCapture();

            Point currentPos = e.GetPosition(TransformWrapper);
            double dx = currentPos.X - _lastMouse.X;
            double dy = currentPos.Y - _lastMouse.Y;
            if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5) return;

            Point canvasPoint = e.GetPosition(FloorSvgCanvas);
            TrySelectRoomAtPoint(canvasPoint);
        }


        private void TrySelectRoomAtPoint(Point canvasPoint)
        {
            double canvasWidth = FloorSvgCanvas.ActualWidth;
            double canvasHeight = FloorSvgCanvas.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            double svgWidth = 210;
            double svgHeight = 140;

            double svgX = canvasPoint.X / canvasWidth * svgWidth;
            double svgY = canvasPoint.Y / canvasHeight * svgHeight;

            Point svgPoint = new Point(svgX, svgY);

            var allRooms = rooms.GetAll();
            var allFloors = floors.GetAll();
            var allEmployees = employees.GetAll();
            var allEmployeeRooms = employeeRooms.GetAll();
            var allEmployeeSubjects = employeeSubjects.GetAll();
            var allSubjects = subjects.GetAll();

            foreach (var kvp in _roomsById)
            {
                string svgId = kvp.Key;
                GeometryDrawing geo = kvp.Value;

                if (geo.Geometry == null) continue;
                if (!geo.Geometry.Bounds.Contains(svgPoint)) continue;
                if (!geo.Geometry.FillContains(svgPoint)) continue;

                var room = allRooms.FirstOrDefault(r => r.SvgId == svgId);
                if (room == null) continue;

                var floor = allFloors.FirstOrDefault(f => f.Id == room.FloorId);
                var empRoom = allEmployeeRooms.FirstOrDefault(er => er.RoomId == room.Id);
                var employee = empRoom != null
                    ? allEmployees.FirstOrDefault(emp => emp.Id == empRoom.EmployeeId)
                    : null;
                var empSubject = employee != null
                    ? allEmployeeSubjects.FirstOrDefault(es => es.EmployeeId == employee.Id)
                    : null;
                var subject = empSubject != null
                    ? allSubjects.FirstOrDefault(s => s.Id == empSubject.SubjectId)
                    : null;

                var result = new RoomSearchResult
                {
                    SvgId = room.SvgId,
                    Name = room.Name,
                    FloorName = floor?.Name ?? "Nezināms stāvs",
                    Type = room.Type ?? "",
                    AssignedEmployee = employee != null
                        ? $"{employee.First_Name} {employee.Last_Name}"
                        : "Nav piešķirts darbinieks"
                };

                ClearHighlight();
                HighlightGeometry(geo);

                SearchTextBox.TextChanged -= SearchTextBox_TextChanged;
                SearchTextBox.Text = room.Name;
                SearchTextBox.TextChanged += SearchTextBox_TextChanged;

                SuggestionPopup.IsOpen = false;
                _selectedRoom = result;
                ShowRoomInfoPanel(result, subject?.name);
                NavigateButton.Visibility = Visibility.Visible;
                return;
            }
        }

        private void ShowRoomInfoPanel(RoomSearchResult result, string subjectName)
        {
            InfoRoomName.Text = result.Name;
            InfoFloorName.Text = result.FloorName;
            InfoRoomType.Text = string.IsNullOrEmpty(result.Type) ? "Nav norādīts" : result.Type;
            InfoEmployeeName.Text = result.AssignedEmployee;
            InfoSubjectName.Text = string.IsNullOrEmpty(subjectName) ? "Nav piešķirts priekšmets" : subjectName;

            if (_editingMode)
            {
                RoomEditPanel.Visibility = Visibility.Visible;
                PopulateEditFields(result);
            }
            else
            {
                RoomEditPanel.Visibility = Visibility.Collapsed;
            }

            RoomInfoPanel.Visibility = Visibility.Visible;
        }

        private void CloseRoomInfoPanel()
        {
            RoomInfoPanel.Visibility = Visibility.Collapsed;
            NavigateButton.Visibility = Visibility.Collapsed;
            _selectedRoom = null;
            _navigationSegments.Clear();
            ClearNavigationPath();
            ClearHighlight();
            SearchTextBox.TextChanged -= SearchTextBox_TextChanged;
            SearchTextBox.Text = "";
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
        }

        private void CloseInfoPanel_Click(object sender, RoutedEventArgs e)
        {
            ClearHighlight();
            CloseRoomInfoPanel();
        }


        //Floor highlights
        private void HighlightGeometry(GeometryDrawing geo)
        {
            if (_highlightedGeometry != null && _originalBrush != null)
                _highlightedGeometry.Brush = _originalBrush;

            _originalBrush = geo.Brush;
            _highlightedGeometry = geo;

            geo.Brush = new SolidColorBrush(Color.FromArgb(180, 255, 100, 100));

            FloorSvgCanvas.InvalidateVisual();
        }

        private void ClearHighlight()
        {
            if (_highlightedGeometry != null && _originalBrush != null)
            {
                _highlightedGeometry.Brush = _originalBrush;
                _highlightedGeometry = null;
                _originalBrush = null;
                FloorSvgCanvas.InvalidateVisual();
            }
        }

        private async void NavigateToFloorAndHighlight(RoomSearchResult selected, bool navigate = false)
        {
            var allRooms = rooms.GetAll();
            var allFloors = floors.GetAll();

            var room = allRooms.FirstOrDefault(r => r.SvgId == selected.SvgId);
            var floor = allFloors.FirstOrDefault(f => f.Id == room?.FloorId);

            if (floor == null)
            {
                MessageBox.Show("Nav atrasts stāvs šim kabinetam.");
                return;
            }

            if (_currentFloor == floor.SVGFile)
            {
                if (_roomsById.TryGetValue(selected.SvgId, out var geo))
                    HighlightGeometry(geo);
                return;
            }

            await SwitchToFloor(floor.SVGFile);
            await Task.Delay(500);

            if (_roomsById.TryGetValue(selected.SvgId, out var geometry))
                HighlightGeometry(geometry);
            else
                MessageBox.Show($"Nevar atrast '{selected.Name}' kartē.");
        }

        //Zooming
        private void Map_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

            double newScaleX = MapScaleTransform.ScaleX * zoomFactor;
            double newScaleY = MapScaleTransform.ScaleY * zoomFactor;


            if (newScaleX < 0.5 || newScaleX > 10) return;

            Point mousePosition = e.GetPosition(TransformWrapper);

            double absoluteX = (mousePosition.X - MapTranslateTransform.X) / MapScaleTransform.ScaleX;
            double absoluteY = (mousePosition.Y - MapTranslateTransform.Y) / MapScaleTransform.ScaleY;

            MapScaleTransform.ScaleX = newScaleX;
            MapScaleTransform.ScaleY = newScaleY;

            MapTranslateTransform.X = mousePosition.X - absoluteX * MapScaleTransform.ScaleX;
            MapTranslateTransform.Y = mousePosition.Y - absoluteY * MapScaleTransform.ScaleY;

            ClampTranslation();
        }

        //User Limiter 
        private void ClampTranslation()
        {
            var drawing = FloorSvgCanvas.Drawings as DrawingGroup;
            if (drawing == null) return;

            double containerWidth = TransformWrapper.ActualWidth;
            double containerHeight = TransformWrapper.ActualHeight;

            double scaledWidth = FloorSvgCanvas.ActualWidth * MapScaleTransform.ScaleX;
            double scaledHeight = FloorSvgCanvas.ActualHeight * MapScaleTransform.ScaleY;

            // How far the map can travel — always keep at least half the map visible
            double minX = containerWidth - scaledWidth - containerWidth * 0.5;
            double maxX = containerWidth * 0.5;
            double minY = containerHeight - scaledHeight - containerHeight * 0.5;
            double maxY = containerHeight * 0.5;

            MapTranslateTransform.X = Math.Max(minX, Math.Min(maxX, MapTranslateTransform.X));
            MapTranslateTransform.Y = Math.Max(minY, Math.Min(maxY, MapTranslateTransform.Y));
        }

        //Exit Program with Q key
        private void Quit_Map()
        {
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            this.Focusable = true;
            this.Focus();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q)
            {
                Application.Current.Shutdown();
            }
        }

        //Floor selection handler
        private void FloorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {


            try
            {
                MapScaleTransform.ScaleX = 1;
                MapScaleTransform.ScaleY = 1;
                MapTranslateTransform.X = 0;
                MapTranslateTransform.Y = 0;

                string building = (BuildingComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string floor = (FloorComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

                ClearNavigationPath();

                if (building == null || floor == null) return;

                string svgFile = null;

                if (building == "Administratīvais korpuss")
                {
                    if (floor == "Pirmais stāvs") svgFile = "first_floor.svg";
                    else if (floor == "Otrais stāvs") svgFile = "second_floor.svg";
                    else if (floor == "Trešais stāvs") svgFile = "third_floor.svg";
                }
                else if (building == "Laboratorijas korpuss")
                {
                    if (floor == "Galvenā ieeja") svgFile = "main_entrance_floor.svg";
                    else if (floor == "Pirmais stāvs") svgFile = "2_first_floor.svg";
                    else if (floor == "Otrais stāvs") svgFile = "2_second_floor.svg";
                    else if (floor == "Trešais stāvs") svgFile = "2_third_floor.svg";
                    else if (floor == "Ceturtais stāvs") svgFile = "2_fourth_floor.svg";
                    else if (floor == "Piektais stāvs") svgFile = "2_fifth_floor.svg";
                    else if (floor == "Sestais stāvs") svgFile = "2_sixth_floor.svg";
                }
                else if (building == "Darbnīcas")
                {
                    if (floor == "Darbnīcas") svgFile = "workshop_floor.svg";
                }

                if (svgFile == null)
                {
                    MessageBox.Show("Nav atrasts attiecīgs SVG fails");
                    return;
                }

                _currentFloor = svgFile;
                FloorSvgCanvas.Source = new Uri($"/Floors/{svgFile}", UriKind.Relative);

                Dispatcher.InvokeAsync(async () =>
                {
                    ReloadSvgAndExtract();
                    Return_ToPosition();

                    await Task.Delay(300);
                    if (_navigationSegments.ContainsKey(_currentFloor))
                    {
                        var allNavigation = navigation.GetAll();
                        var floorNav = allNavigation.FirstOrDefault(n => n.SVGFile == _currentFloor);
                        if (floorNav != null)
                            DrawPath(_navigationSegments[_currentFloor], floorNav.Waypoints);
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);

                if (_showingWaypoints)
                {
                    ClearWaypointDebug();
                    DrawWaypointDebug();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading floor: {ex.Message}");
            }
        }

        //Return to Starting position and scale
        private void Return_ToPosition()
        {
            var drawing = FloorSvgCanvas.Drawings as DrawingGroup;
            if (drawing == null)
                return;

            Rect bounds = drawing.Bounds;

            double containerWidth = TransformWrapper.ActualWidth;
            double containerHeight = TransformWrapper.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
                return;

            double scaleX = containerWidth / bounds.Width;
            double scaleY = containerHeight / bounds.Height;

            double scale = Math.Min(scaleX, scaleY);

            MapScaleTransform.ScaleX = scale;
            MapScaleTransform.ScaleY = scale;


            MapTranslateTransform.X =
                (containerWidth - bounds.Width * scale) / 2 - bounds.X * scale;

            MapTranslateTransform.Y =
                (containerHeight - bounds.Height * scale) / 2 - bounds.Y * scale;
        }

        private void ReturnToOriginButton_Click(object sender, RoutedEventArgs e)
        {
            BuildingComboBox.SelectedIndex = 0; // Administratīvais korpuss
            FloorComboBox.SelectedIndex = 0;    // Pirmais stāvs


            MapScaleTransform.ScaleX = 1;
            MapScaleTransform.ScaleY = 1;
            MapTranslateTransform.X = 0;
            MapTranslateTransform.Y = 0;
        }

        //Count Geometries
        private void ExtractGeometries(DrawingGroup drawingGroup)
        {
            foreach (var drawing in drawingGroup.Children)
            {
                if (drawing is GeometryDrawing geo)
                {
                    _allGeometries.Add(geo);

                    string id = SvgObject.GetId(geo);

                    if (!string.IsNullOrEmpty(id))
                    {
                        _roomsById[id] = geo;
                        System.Diagnostics.Debug.WriteLine($"Found geometry with ID: {id}");
                    }
                }
                else if (drawing is DrawingGroup childGroup)
                {
                    ExtractGeometries(childGroup);
                }

            }
        }

        //Search function handlers
        private List<RoomSearchResult> BuildSearchResults(string filter)
        {
            var allRooms = rooms.GetAll();
            var allFloors = floors.GetAll();
            var allEmployees = employees.GetAll();
            var allEmployeeRooms = employeeRooms.GetAll();
            var allEmployeeSubjects = employeeSubjects.GetAll();
            var allSubjects = subjects.GetAll();

            var results = new List<RoomSearchResult>();

            foreach (var r in allRooms)
            {
                var floor = allFloors.FirstOrDefault(f => f.Id == r.FloorId);
                var empRoom = allEmployeeRooms.FirstOrDefault(er => er.RoomId == r.Id);
                var employee = empRoom != null
                    ? allEmployees.FirstOrDefault(e => e.Id == empRoom.EmployeeId)
                    : null;
                var empSubject = employee != null
                    ? allEmployeeSubjects.FirstOrDefault(es => es.EmployeeId == employee.Id)
                    : null;
                var subject = empSubject != null
                    ? allSubjects.FirstOrDefault(s => s.Id == empSubject.SubjectId)
                    : null;

                string employeeFullName = employee != null
                    ? $"{employee.First_Name} {employee.Last_Name}"
                    : "";
                string subjectName = subject?.name ?? "";
                string roomType = r.Type ?? "";

                bool matches =
                    r.Name.ToLower().Contains(filter) ||
                    employeeFullName.ToLower().Contains(filter) ||
                    subjectName.ToLower().Contains(filter) ||
                    roomType.ToLower().Contains(filter);

                if (!matches) continue;

                results.Add(new RoomSearchResult
                {
                    SvgId = r.SvgId,
                    Name = r.Name,
                    FloorName = floor?.Name ?? "Nezināms stāvs",
                    Type = string.IsNullOrEmpty(roomType) ? "" : roomType,
                    AssignedEmployee = string.IsNullOrEmpty(employeeFullName)
                        ? "Nav piešķirts darbinieks"
                        : employeeFullName
                });
            }

            return results;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string typed = SearchTextBox.Text.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(typed))
            {
                SuggestionPopup.IsOpen = false;
                _navigationSegments.Clear();
                ClearNavigationPath();
                ClearHighlight();
                RoomInfoPanel.Visibility = Visibility.Collapsed;
                NavigateButton.Visibility = Visibility.Collapsed;
                _selectedRoom = null;
                return;
            }

            var results = BuildSearchResults(typed);

            if (results.Any())
            {
                SuggestionBox.ItemsSource = results;
                SuggestionPopup.IsOpen = true;
            }
            else
            {
                SuggestionPopup.IsOpen = false;
            }
        }

        private void SuggestionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionBox.SelectedItem is RoomSearchResult selected)
            {
                SearchTextBox.Text = selected.Name;
                SuggestionPopup.IsOpen = false;

                _selectedRoom = selected;

                NavigateToFloorAndHighlight(selected, navigate: false);
                NavigateButton.Visibility = Visibility.Visible;

                var allEmployees = employees.GetAll();
                var allEmployeeRooms = employeeRooms.GetAll();
                var allEmployeeSubjects = employeeSubjects.GetAll();
                var allSubjects = subjects.GetAll();

                var empRoom = allEmployeeRooms.FirstOrDefault(er => er.RoomId ==
                    rooms.GetAll().FirstOrDefault(r => r.SvgId == selected.SvgId)?.Id);
                var employee = empRoom != null
                    ? allEmployees.FirstOrDefault(emp => emp.Id == empRoom.EmployeeId)
                    : null;
                var empSubject = employee != null
                    ? allEmployeeSubjects.FirstOrDefault(es => es.EmployeeId == employee.Id)
                    : null;
                var subject = empSubject != null
                    ? allSubjects.FirstOrDefault(s => s.Id == empSubject.SubjectId)
                    : null;

                ShowRoomInfoPanel(selected, subject?.name);
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string typed = SearchTextBox.Text.Trim();

                if (typed == password)
                {
                    _editingMode = !_editingMode;
                    MessageBox.Show(_editingMode
                        ? "Ieslēgts rediģēšanas režīms"
                        : "Izslēgts rediģēšanas režīms");
                    SearchTextBox.Text = "";
                    return;
                }

                string lowerTyped = typed.ToLower();
                var results = BuildSearchResults(lowerTyped);
                var exact = results.FirstOrDefault(r => r.Name.ToLower() == lowerTyped);


                if (exact != null)
                {
                    SuggestionPopup.IsOpen = false;
                    _selectedRoom = exact;
                    NavigateToFloorAndHighlight(exact, navigate: false);
                    NavigateButton.Visibility = Visibility.Visible;
                }
                else if (string.IsNullOrEmpty(typed))
                {
                    MessageBox.Show("Lūdzu ievadiet telpas nosaukumu, numuru vai atbildīgās personas vārdu vai uzvārdu, lai meklētu telpu!");
                }
                else
                {
                    MessageBox.Show($"Room '{SearchTextBox.Text}' not found.");
                }
            }
        }
        private void ReloadSvgAndExtract()
        {
            Dispatcher.InvokeAsync(() =>
            {
                var drawing = FloorSvgCanvas.Drawings as DrawingGroup;

                if (drawing != null)
                {
                    _allGeometries.Clear();
                    _roomsById.Clear();
                    _highlightedGeometry = null;
                    _originalBrush = null;

                    ExtractGeometries(drawing);

                    System.Diagnostics.Debug.WriteLine("Geometries extracted for new floor.");
                }

            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        //Bulding and Floor ComboBox handlers
        private void BuildingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FloorComboBox == null) return;

            string selected = (BuildingComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            FloorComboBox.Items.Clear();

            switch (selected)
            {
                case "Administratīvais korpuss":
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Pirmais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Otrais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Trešais stāvs" });
                    break;

                case "Laboratorijas korpuss":
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Galvenā ieeja" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Pirmais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Otrais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Trešais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Ceturtais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Piektais stāvs" });
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Sestais stāvs" });

                    break;
                case "Darbnīcas":
                    FloorComboBox.Items.Add(new ComboBoxItem { Content = "Darbnīcas" });
                    break;
            }

            FloorComboBox.SelectedIndex = 0;
        }

        //Navigation handlers

        private List<NavStep> FindPath(string startFloor, string targetRoomSvgId)
        {
            var allNavigation = navigation.GetAll();
            System.Diagnostics.Debug.WriteLine($"FindPath: startFloor={startFloor}, target={targetRoomSvgId}");
            System.Diagnostics.Debug.WriteLine($"Navigation floors loaded: {string.Join(", ", allNavigation.Select(n => n.SVGFile))}");

            string targetFloor = null;
            string targetWaypointId = null;

            foreach (var floorNav in allNavigation)
            {
                if (floorNav.RoomEntries.TryGetValue(targetRoomSvgId, out var wpId))
                {
                    targetFloor = floorNav.SVGFile;
                    targetWaypointId = wpId;
                    break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Target floor: {targetFloor}, Target waypoint: {targetWaypointId}");

            if (targetFloor == null)
            {
                System.Diagnostics.Debug.WriteLine("Target room not found in any floor's RoomEntries!");
                return null;
            }
            var queue = new Queue<List<NavStep>>();
            var visited = new HashSet<string>();

            var startFloorNav = allNavigation.FirstOrDefault(n => n.SVGFile == startFloor);

            System.Diagnostics.Debug.WriteLine($"Start floor nav found: {startFloorNav != null}");
            System.Diagnostics.Debug.WriteLine($"Start floor waypoints: {string.Join(", ", startFloorNav?.Waypoints.Select(w => w.Id) ?? new List<string>())}");

            if (startFloorNav == null)
                return null;

            string startWaypointId = startFloorNav.Waypoints.Any(w => w.Id == "origin")
            ? "origin"
            : startFloorNav.Waypoints.First().Id;

            var startStep = new NavStep { WaypointId = startWaypointId, FloorSVG = startFloor };
            queue.Enqueue(new List<NavStep> { startStep });
            visited.Add($"{startFloor}:{startWaypointId}");

            while (queue.Count > 0)
            {
                try
                {


                    var path = queue.Dequeue();
                    var current = path.Last();

                    if (current.FloorSVG == targetFloor && current.WaypointId == targetWaypointId)
                        return path;

                    var floorNav = allNavigation.FirstOrDefault(n => n.SVGFile == current.FloorSVG);
                    if (floorNav == null)
                        continue;

                    var waypointMap = floorNav.Waypoints.GroupBy(w => w.Id)
                        .ToDictionary(g => g.Key, g => g.First());

                    if (!waypointMap.ContainsKey(current.WaypointId))
                        continue;

                    var wp = waypointMap[current.WaypointId];

                    foreach (var neighbor in wp.Connections)
                    {
                        string key = $"{current.FloorSVG}:{neighbor}";
                        if (!visited.Contains(key))
                        {
                            visited.Add(key);
                            var newPath = new List<NavStep>(path)
                        {
                            new NavStep { WaypointId = neighbor, FloorSVG = current.FloorSVG}
                        };
                            queue.Enqueue(newPath);

                        }
                    }

                    if (wp.TransitionToFloor != null && wp.TransitionToWaypoint != null)
                    {
                        string key = $"{wp.TransitionToFloor}:{wp.TransitionToWaypoint}";
                        if (!visited.Contains(key))
                        {
                            visited.Add(key);
                            var newPath = new List<NavStep>(path)
                        {
                            new NavStep {WaypointId = wp.TransitionToWaypoint, FloorSVG = wp.TransitionToFloor}
                        };
                            queue.Enqueue(newPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"FindPath EXCEPTION: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        //Navigation path drawing
        private void DrawPath(List<string> path, List<Waypoint> waypoints)
        {
            //Remove old path
            ClearNavigationPath();

            //Waypoint map for quick access to coordinates
            var waypointMap = waypoints.ToDictionary(w => w.Id);

            for (int i = 0; i < path.Count - 1; i++)
            {
                var from = waypointMap[path[i]];
                var to = waypointMap[path[i + 1]];

                //Path drawing with red dashed line
                var line = new Line
                {
                    X1 = SvgToCanvas(from.X, true),
                    Y1 = SvgToCanvas(from.Y, false),
                    X2 = SvgToCanvas(to.X, true),
                    Y2 = SvgToCanvas(to.Y, false),
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Tag = "navPath",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                //Add line to the map
                MapTransformGrid.Children.Add(line);
            }
        }

        //Clear navigation path from the map
        private void ClearNavigationPath()
        {
            //Finds all the lines with the tag "navPath" and adds them to the "toRemove" list
            var toRemove = MapTransformGrid.Children
                .OfType<Line>()
                .Where(l => l.Tag?.ToString() == "navPath")
                .ToList();

            //Removes lines from the map
            foreach (var line in toRemove)
                MapTransformGrid.Children.Remove(line);
        }

        //Convert SVG coordinates to canvas coordinates
        private double SvgToCanvas(double value, bool isX)
        {
            //SVG file dimensions
            double svgWidth = 210;
            double svgHeight = 140;

            var drawing = FloorSvgCanvas.Drawings as DrawingGroup;
            if (drawing == null) return value;

            Rect bounds = drawing.Bounds;

            //Calculate canvas coordinates based on SVG coordinates
            double scaleX = bounds.Width / svgWidth;
            double scaleY = bounds.Height / svgHeight;

            //Return the converted coordinate
            return isX
                ? value * scaleX + bounds.X
                : value * scaleY + bounds.Y;
        }

        //Path drawing

        private async void ExecuteMultiFloorNavigation(List<NavStep> fullPath)
        {
            try
            {
                var allNavigation = navigation.GetAll();
                var allFloors = floors.GetAll();
                var segments = new List<(string floor, List<NavStep> steps)>();
                string currentSegmentFloor = null;
                List<NavStep> currentSegment = null;

                _navigationSegments.Clear();

                foreach (var step in fullPath)
                {
                    if (step.FloorSVG != currentSegmentFloor)
                    {
                        if (currentSegment != null)
                            segments.Add((currentSegmentFloor, currentSegment));

                        currentSegmentFloor = step.FloorSVG;
                        currentSegment = new List<NavStep>();
                    }
                    currentSegment.Add(step);
                }
                if (currentSegment != null)
                    segments.Add((currentSegmentFloor, currentSegment));

                var routeSteps = new List<RouteStepDisplay>();
                foreach (var (floor, steps) in segments)
                {
                    var floorData = allFloors.FirstOrDefault(f => f.SVGFile == floor);
                    string floorName = floorData?.Name ?? floor;

                    var lastStep = steps.Last();
                    var floorNav = allNavigation.FirstOrDefault(n => n.SVGFile == floor);
                    var lastWp = floorNav?.Waypoints.FirstOrDefault(w => w.Id == lastStep.WaypointId);
                    bool isTransition = lastWp?.TransitionToFloor != null;

                    routeSteps.Add(new RouteStepDisplay
                    {
                        Description = isTransition
                            ? $"🚶 {floorName} → kāpnes"
                            : $"🏁 {floorName}",
                        Color = isTransition ? Brushes.Orange : Brushes.LightGreen
                    });
                }


                for (int i = 0; i < segments.Count; i++)
                {
                    var (floor, steps) = segments[i];
                    bool isLastSegment = i == segments.Count - 1;

                    if (_currentFloor != floor)
                        await SwitchToFloor(floor);

                    await Task.Delay(300);

                    var waypointIds = steps.Select(s => s.WaypointId).ToList();
                    var floorNav = allNavigation.FirstOrDefault(n => n.SVGFile == floor);
                    if (floorNav != null)
                        DrawPath(waypointIds, floorNav.Waypoints);

                    if (isLastSegment && _selectedRoom != null)
                    {
                        if (_roomsById.TryGetValue(_selectedRoom.SvgId, out var geo))
                            HighlightGeometry(geo);
                    }

                    if (!isLastSegment)
                        await Task.Delay(1500);
                }

                foreach (var (floor, steps) in segments)
                {
                    var waypointIds = steps.Select(s => s.WaypointId).ToList();
                    _navigationSegments[floor] = waypointIds;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteMultiFloorNavigation EXCEPTION: {ex.Message}");
                MessageBox.Show($"Navigation error: {ex.Message}");
            }
        }

        private async Task SwitchToFloor(string svgFile)
        {
            var allFloors = floors.GetAll();
            var floor = allFloors.FirstOrDefault(f => f.SVGFile == svgFile);
            if (floor == null)
                return;

            foreach (ComboBoxItem item in BuildingComboBox.Items)
            {
                if (item.Content.ToString() == floor.BuildingName)
                {
                    BuildingComboBox.SelectedItem = item;
                    break;
                }
            }

            foreach (ComboBoxItem item in FloorComboBox.Items)
            {
                if (item.Content.ToString() == floor.Name)
                {
                    FloorComboBox.SelectedItem = item;
                    break;
                }
            }

            await Task.Delay(500);
        }

        //Navigation button handler

        private void NavigateButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"_currentFloor = {_currentFloor}");
            System.Diagnostics.Debug.WriteLine($"_selectedRoom = {_selectedRoom?.SvgId}");
            if (_selectedRoom == null)
                return;

            string startFloor = "first_floor.svg";

            var fullPath = FindPath(startFloor, _selectedRoom.SvgId);

            if (fullPath == null)
            {
                MessageBox.Show("Nevar atrast ceļu uz šo kabinetu");
                return;
            }

            ExecuteMultiFloorNavigation(fullPath);

        }

        //Waypoint test handler

        private void DrawWaypointDebug()
        {
            var allNavigation = navigation.GetAll();
            var floorNav = allNavigation.FirstOrDefault(n => n.SVGFile == _currentFloor);

            if (floorNav == null) return;

            foreach (var wp in floorNav.Waypoints)
            {
                double x = SvgToCanvas(wp.X, true);
                double y = SvgToCanvas(wp.Y, false);

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.Red,
                    Tag = "wpdebug",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(x - 4, y - 4, 0, 0)
                };

                var label = new TextBlock
                {
                    Text = wp.Id,
                    Foreground = Brushes.Blue,
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Tag = "wpdebug",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(x + 5, y - 8, 0, 0)
                };

                MapTransformGrid.Children.Add(dot);
                MapTransformGrid.Children.Add(label);
            }
        }

        private void ClearWaypointDebug()
        {
            var toRemove = MapTransformGrid.Children
                .OfType<UIElement>()
                .Where(el => (el as FrameworkElement)?.Tag?.ToString() == "wpdebug")
                .ToList();

            foreach (var el in toRemove)
                MapTransformGrid.Children.Remove(el);
        }

        private void DebugWaypointsButton_Click(object sender, RoutedEventArgs e)
        {
            _showingWaypoints = !_showingWaypoints;

            if (_showingWaypoints)
            {
                DrawWaypointDebug();
                DebugWaypointsButton.Content = "Hide waypoints";
            }
            else
            {
                ClearWaypointDebug();
                DebugWaypointsButton.Content = "Show waypoints";
            }
        }



        //Employee management button handlers
        private void ViewEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            var viewWindow = new ViewEmployees(employees, employeeRooms, rooms, subjects, employeeSubjects, _editingMode);
            viewWindow.ShowDialog();
        }

        //Room management button handlers
        private void PopulateEditFields(RoomSearchResult result)
        {

            var allEmployees = employees.GetAll();
            EditEmployeeCombo.Items.Clear();
            EditEmployeeCombo.Items.Add(new ComboBoxItem
            {
                Content = "Nav piešķirts",
                Tag = -1
            });

            foreach (var emp in allEmployees)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{emp.First_Name} {emp.Last_Name}",
                    Tag = emp.Id
                };
                EditEmployeeCombo.Items.Add(item);

                if (result.AssignedEmployee == $"{emp.First_Name} {emp.Last_Name}")
                    EditEmployeeCombo.SelectedItem = item;
            }

            if (EditEmployeeCombo.SelectedIndex == -1)
                EditEmployeeCombo.SelectedIndex = 0;

            var allSubjects = subjects.GetAll();
            EditSubjectCombo.Items.Clear();
            EditSubjectCombo.Items.Add(new ComboBoxItem
            {
                Content = "Nav piešķirts",
                Tag = -1
            });

            foreach (var subject in allSubjects)
            {
                var item = new ComboBoxItem
                {
                    Content = subject.name,
                    Tag = subject.Id
                };
                EditSubjectCombo.Items.Add(item);

                if (InfoSubjectName.Text == subject.name)
                    EditSubjectCombo.SelectedItem = item;
            }

            if (EditSubjectCombo.SelectedIndex == -1)
                EditSubjectCombo.SelectedIndex = 0;
        }

        private void SaveRoomButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRoom == null) return;

            var allRooms = rooms.GetAll();
            var room = allRooms.FirstOrDefault(r => r.SvgId == _selectedRoom.SvgId);
            if (room == null) return;

            rooms.SaveAll(allRooms);

            var allEmpRooms = employeeRooms.GetAll();
            allEmpRooms.RemoveAll(er => er.RoomId == room.Id);

            int selectedEmpId = (int)(EditEmployeeCombo.SelectedItem as ComboBoxItem)?.Tag;
            if (selectedEmpId > 0)
            {
                allEmpRooms.Add(new EmployeeRoom
                {
                    EmployeeId = selectedEmpId,
                    RoomId = room.Id
                });
            }
            employeeRooms.SaveAll(allEmpRooms);

            if (selectedEmpId > 0)
            {
                var allEmpSubjects = employeeSubjects.GetAll();
                allEmpSubjects.RemoveAll(es => es.EmployeeId == selectedEmpId);

                int selectedSubjectId = (int)(EditSubjectCombo.SelectedItem as ComboBoxItem)?.Tag;
                if (selectedSubjectId > 0)
                {
                    allEmpSubjects.Add(new EmployeeSubject
                    {
                        EmployeeId = selectedEmpId,
                        SubjectId = selectedSubjectId
                    });
                }
                employeeSubjects.SaveAll(allEmpSubjects);
            }
        }
    }
}