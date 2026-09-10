using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SkolasCelvedis
{
    public class Models
    {
        /// <summary>
        /// Every class in this is purely for the purpose
        /// of turning this into Json repository and storing it in a file.
        /// When the search function is used it uses the json file to find
        /// the necessary search.
        /// </summary>
        public class Floor
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string SVGFile { get; set; }
            public string BuildingName { get; set; }
            public string FloorName { get; set; }
        }

        public class Room
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int FloorId { get; set; }
            public string Type { get; set; }
            public string SvgId { get; set; } 
        }

        public class Employee
        {
            public int Id { get; set; }
            public string First_Name {  get; set; }
            public string Last_Name { get; set; }
        }

        public class  Subject
        {
            public int Id { get; set; }
            public string name { get; set; }
        }

        public class EmployeeSubject
        {
            public int EmployeeId { get; set; }
            public int SubjectId { get; set; }
        }
        
        public class EmployeeRoom
        {
            public int EmployeeId { get; set; }
            public int RoomId { get; set; } = 0;
        }

        public class RoomSearchResult
        {
            public string SvgId { get; set; }
            public string Name { get; set; }
            public string FloorName { get; set; }
            public string Type { get; set; }
            public string AssignedEmployee { get; set; }
        }

        public class Waypoint
        {
            public string Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public List<string> Connections { get; set; } = new List<string>();

            public string TransitionToFloor { get; set; } = null;
            public string TransitionToWaypoint { get; set; } = null;
        }

        public class FloorNavigation
        {
            public string SVGFile { get; set; }
            public List<Waypoint> Waypoints { get; set; } = new List<Waypoint>();
            public Dictionary<string, string> RoomEntries { get; set; } = new Dictionary<string, string>();
        }

        public class NavStep
        {
            public string WaypointId { get; set; }
            public string FloorSVG { get; set; }
        }

        public class RouteStepDisplay
        {
            public string Description { get; set; }
            public Brush Color { get; set; }
        }
    }
}
