# [SkyComb Drone Library](https://github.com/PhilipQuirke/SkyCombDroneLibrary/) 

SkyComb Drone Library is a library that:
- takes as input the flight log (SRT) file created by a drone during a flight
- integrates ground data provided by the SkyComb Ground library
- generates a summary of the drone flight and saves it to a datastore (xls)   

This "drone data" library is included in the tools:
- [SkyComb Analyst](https://github.com/PhilipQuirke/SkyCombAnalyst/) 
- [SkyComb Flights](https://github.com/PhilipQuirke/SkyCombFlights/)

The folders are:
- CommonSpace: Constants and generic code shared by SkyCombGroundLibrary, SkyCombDroneLibrary, SkyCombFlights & SkyCombAnalyst
- DroneModel: In-memory representations (models) of drone flight objects including sections, steps, legs,
- DroneLogic: Logic on how to parse flight logs, integrate ground data, correct flight data, and summarise the flight data    
- PersistModel: Save/load drone data from/to the datastore (xls)
- DrawSpace: Code to draw graphs, charts, images containing drone and/or ground data.
