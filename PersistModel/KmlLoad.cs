using System.Xml.Linq;


namespace SkyCombDrone.PersistModel
{
    public class Pin
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double? Altitude { get; set; }
    }

    public class DroneFlight
    {
        public string Name { get; set; }
        public List<(double Longitude, double Latitude, double? Altitude)> Path { get; set; }
    }

    public class KmlLoader
    {
        private static readonly XNamespace KmlNs = "http://www.opengis.net/kml/2.2";

        public static List<DroneFlight> LoadDroneFlights(string kmlPath)
        {
            var flights = new List<DroneFlight>();
            XDocument kmlDoc = XDocument.Load(kmlPath);

            foreach (var placemark in kmlDoc.Descendants(KmlNs + "Placemark"))
            {
                var lineString = placemark.Element(KmlNs + "LineString");
                if (lineString != null)
                {
                    var coordinatesText = lineString.Element(KmlNs + "coordinates")?.Value;
                    if (!string.IsNullOrWhiteSpace(coordinatesText))
                    {
                        var coords = ParseCoordinates(coordinatesText);
                        flights.Add(new DroneFlight
                        {
                            Name = placemark.Element(KmlNs + "name")?.Value ?? "Unnamed Flight",
                            Path = coords
                        });
                    }
                }
            }

            return flights;
        }

        public static List<Pin> LoadPins(string kmlPath)
        {
            var pins = new List<Pin>();
            XDocument kmlDoc = XDocument.Load(kmlPath);

            foreach (var placemark in kmlDoc.Descendants(KmlNs + "Placemark"))
            {
                var point = placemark.Element(KmlNs + "Point");
                if (point != null)
                {
                    var coordinatesText = point.Element(KmlNs + "coordinates")?.Value;
                    if (!string.IsNullOrWhiteSpace(coordinatesText))
                    {
                        var parts = coordinatesText.Split(',').Select(x => double.Parse(x)).ToArray();
                        pins.Add(new Pin
                        {
                            Name = placemark.Element(KmlNs + "name")?.Value ?? "Unnamed Pin",
                            Description = placemark.Element(KmlNs + "description")?.Value ?? "",
                            Longitude = parts[0],
                            Latitude = parts[1],
                            Altitude = parts.Length > 2 ? parts[2] : (double?)null
                        });
                    }
                }
            }

            return pins;
        }

        private static List<(double Longitude, double Latitude, double? Altitude)> ParseCoordinates(string coordinatesText)
        {
            var coords = new List<(double, double, double?)>();
            var points = coordinatesText.Trim().Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var point in points)
            {
                var parts = point.Split(',');
                if (parts.Length >= 2)
                {
                    double lon = double.Parse(parts[0]);
                    double lat = double.Parse(parts[1]);
                    double? alt = parts.Length >= 3 ? double.Parse(parts[2]) : (double?)null;
                    coords.Add((lon, lat, alt));
                }
            }

            return coords;
        }
    }
}
