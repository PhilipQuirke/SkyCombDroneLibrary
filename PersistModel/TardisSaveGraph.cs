// Copyright SkyComb Limited 2025. All rights reserved. 


namespace SkyCombDrone.PersistModel
{
    // Save graphs about a sequence of tardis datums
    public class TardisSaveGraph : DataStoreAccessor
    {
        public readonly int ChartWidth = 2 * StandardChartCols;


        public string TardisTabName;
        public string GraphTabName;


        protected int MinDatumId = 0;
        protected int MaxDatumId = 1;


        public TardisSaveGraph(DroneDataStore data, string tardisTabName, string graphTabName) : base(data)
        {
            TardisTabName = tardisTabName;
            GraphTabName = graphTabName;
        }
    }
}