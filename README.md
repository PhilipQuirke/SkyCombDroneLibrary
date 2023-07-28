# [SkyComb Drone Library](https://github.com/PhilipQuirke/SkyCombDroneLibrary/) 

SkyComb Drone Library is a library that:
- takes as input the flight log (SRT) and video files created by a drone during a flight containing drone elevation, location, pitch, etc information
- integrates ground (DEM & DSM) data provided by the SkyComb Ground library
- generates a summary of the drone flight and saves the detail, summary and graphs to a spreadsheet

This "drone data" library is incorporated into the tools:
- [SkyComb Analyst](https://github.com/PhilipQuirke/SkyCombAnalyst/) 
- [SkyComb Flights](https://github.com/PhilipQuirke/SkyCombFlights/)

The folders are:
- **CommonSpace:** Constants and generic code shared by SkyCombDroneLibrary, SkyCombFlights & SkyCombAnalyst
- **DroneModel:** In-memory representations (models) of drone flight objects including sections, steps, legs,
- **DroneLogic:** Logic on how to parse flight logs, integrate ground data, correct flight data, and summarise the flight data    
- **DrawSpace:** Code to draw graphs, charts, images containing drone and/or ground data.
- **PersistModel:** Save/load DroneModel data from/to the datastore (spreadsheet) including graphs
