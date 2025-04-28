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


    public class PinList
    {
        public string KmlPath { get; set; }
        public List<Pin> Pins { get; set; }

        public PinList( string kmlPath )
        {
            KmlPath = kmlPath;
            Pins = new();
        }
    }


    public class KmlLoader
    {
        private static readonly XNamespace KmlNs = "http://www.opengis.net/kml/2.2";


        // Load a list of placemark pins from a KML file
        public static PinList LoadPinList(string kmlPath)
        {
            var pinList = new PinList(kmlPath);

            XDocument kmlDoc = XDocument.Load(kmlPath);

            var placemarks = kmlDoc.Descendants().Where(e => e.Name.LocalName == "Placemark");
            foreach (var placemark in placemarks)
            {
                var name = placemark.Descendants().FirstOrDefault(e => e.Name.LocalName == "name")?.Value ?? "Unnamed Pin";
                var description = placemark.Descendants().FirstOrDefault(e => e.Name.LocalName == "description")?.Value ?? "";
                var pointElement = placemark.Descendants().FirstOrDefault(e => e.Name.LocalName == "Point");

                if (pointElement != null)
                {
                    var coordinatesElement = pointElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates");
                    if (coordinatesElement != null)
                    {
                        var coordinatesText = coordinatesElement.Value;
                        if (!string.IsNullOrWhiteSpace(coordinatesText))
                        {
                            var parts = coordinatesText.Split(',').Select(x => double.Parse(x)).ToArray();

                            pinList.Pins.Add(new Pin
                            {
                                Name = name,
                                Description = description,
                                Longitude = parts[0],
                                Latitude = parts[1],
                                Altitude = parts.Length > 2 ? parts[2] : (double?)null
                            });
                        }
                    }
                }
            }

            return pinList;
        }


        public static List<PinList> LoadPinListList(List<string> kmlPaths)
        {
            // There may be one pin file shared across many flights.
            // So we load all the pins here so they are available below.
            List<PinList> allpins = new();
            foreach (var kmlPath in kmlPaths)
            {
                var pinList = KmlLoader.LoadPinList(kmlPath);
                if (pinList != null)
                    allpins.Add(pinList);
            }
            return allpins;
        }


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
