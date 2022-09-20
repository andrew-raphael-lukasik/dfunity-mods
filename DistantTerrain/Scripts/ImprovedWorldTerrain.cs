//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

// only define CREATE_* if you want to manually create the maps - usually this should not be necessary (and never do in executable build - only from editor - otherwise paths are wrong)
//#define CREATE_PERSISTENT_LOCATION_RANGE_MAPS 
//#define CREATE_PERSISTENT_TREE_COVERAGE_MAP
#define LOAD_TREE_COVERAGE_MAP

using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using Unity.Mathematics;
using Unity.Profiling;

using IO = System.IO;
using Text = System.Text;// for Encoding.UTF8

namespace DistantTerrain
{
    /// <summary> </summary>
    public static class ImprovedWorldTerrain
    {

        const string
            filenameMapLocationRangeX = "mapLocationRangeX.bin",
            filenameMapLocationRangeY = "mapLocationRangeY.bin",
            filenameTreeCoverageMap = "mapTreeCoverage.bin",
            out_filepathMapLocationRangeX = "Game/Addons/DistantTerrain/Resources/mapLocationRangeX_out.bin", // only used on manual trigger in unity editor - never in executable - so it should be ok
            out_filepathMapLocationRangeY = "Game/Addons/DistantTerrain/Resources/mapLocationRangeY_out.bin", //  only used on manual trigger in unity editor - never in executable - so it should be ok
            out_filepathOutTreeCoverageMap = "Game/Addons/DistantTerrain/Resources/mapTreeCoverage_out.bin"; //  only used on manual trigger in unity editor - never in executable - so it should be ok

        const float
            minDistanceFromWaterForExtraExaggeration = 3.0f, // when does exaggeration start in terms of how far does terrain have to be away from water
            exaggerationFactorWaterDistance = 0.075f, // 0.15f; //0.123f; //0.15f; // how strong is the distance from water incorporated into the multiplier
            extraExaggerationFactorLocationDistance = 0.0275f; // how strong is the distance from locations incorporated into the multiplier
        
        public const float
            maxHeightsExaggerationMultiplier = 25.0f, // this directly affects maxTerrainHeight in TerrainHelper.cs: maxTerrainHeight should be maxHeightsExaggerationMultiplier * baseHeightScale * 128 + noiseMapScale * 128 + extraNoiseScale
        // additional height noise based on climate
            additionalHeightNoiseClimateOcean = 0.0f,
            additionalHeightNoiseClimateDesert = 0.85f, //0.35f;
            additionalHeightNoiseClimateDesert2 = 1.05f,
            additionalHeightNoiseClimateMountain = 0.45f,
            additionalHeightNoiseClimateRainforest = 0.77f,
            additionalHeightNoiseClimateSwamp = 1.7f,
            additionalHeightNoiseClimateSubtropical = 0.65f,
            additionalHeightNoiseClimateMountainWoods = 1.05f,
            additionalHeightNoiseClimateWoodlands = 0.59f, //0.45f;
            additionalHeightNoiseClimateHauntedWoodlands = 0.25f;

        private static float[]
            mapDistanceSquaredFromWater = null,// 2D distance transform image - squared distance to water pixels of the world map
            mapDistanceSquaredFromLocations = null,// 2D distance transform image - squared distance to world map pixels with location
            mapMultipliers = null;// map of multiplier values

        private static byte[]
            mapLocations = null,// map with location positions
            mapTreeCoverage = null,// map with tree coverage
            mapLocationRangeX = null,
            mapLocationRangeY = null;

        // indicates if improved terrain is initialized (InitImprovedWorldTerrain() function was called)
        private static bool init = false;
        public static bool IsInit { get { return init; } }

        public static void Unload()
        {
            mapDistanceSquaredFromWater = null;
            mapDistanceSquaredFromLocations = null;
            mapMultipliers = null;
            mapLocations = null;
            mapTreeCoverage = null;
            mapLocationRangeX = null;
            mapLocationRangeY = null;

            //Resources.UnloadUnusedAssets();

            //System.GC.Collect();
        }

        /// <summary>
        /// Gets or sets map with location positions
        /// </summary>
        public static byte[] MapLocations
        {
            get
            {
                return mapLocations;
            }
            set { mapLocations = value; }
        }

        public static byte[] MapTreeCoverage
        {
            get
            {
                return mapTreeCoverage;
            }
            set { mapTreeCoverage = value; }
        }

        public static byte[] MapLocationRangeX
        {
            get
            {
                return mapLocationRangeX;
            }
            set { mapLocationRangeX = value; }
        }

        public static byte[] MapLocationRangeY
        {
            get
            {
                return mapLocationRangeY;
            }
            set { mapLocationRangeY = value; }
        }

        /// <summary>
        /// Gets or sets map with squared distance to water pixels.
        /// </summary>
        public static float[] MapDistanceSquaredFromWater
        {
            get {
                return mapDistanceSquaredFromWater;
            }
            set { mapDistanceSquaredFromWater = value; }
        }

        #region Profiler Markers

        static readonly ProfilerMarker
            ___InitImprovedWorldTerrain = new ProfilerMarker($"{nameof(ImprovedWorldTerrain)}::{nameof(InitImprovedWorldTerrain)}"),
            ___ImageDistanceTransform = new ProfilerMarker($"{nameof(ImprovedWorldTerrain)}::{nameof(ImageDistanceTransform)}"),
            ___DistanceTransform1D = new ProfilerMarker($"{nameof(ImprovedWorldTerrain)}::{nameof(DistanceTransform1D)}"),
            ___DistanceTransform2D = new ProfilerMarker($"{nameof(ImprovedWorldTerrain)}::{nameof(DistanceTransform2D)}");

        #endregion

        /// <summary>
        /// gets the distance to water for a given world map pixel.
        /// </summary>
        public static float getDistanceFromWater(int mapPixelX, int mapPixelY)
        {
            if (init)
            {
                return ((float)math.sqrt(mapDistanceSquaredFromWater[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]));
            }
            else
            {
                DaggerfallUnity.LogMessage("ImprovedWorldTerrain not initialized.", true);
                return (1.0f);
            }
        }

        /// <summary>
        /// computes the height multiplier for a given world map pixel.
        /// </summary>
        public static float computeHeightMultiplier(int mapPixelX, int mapPixelY)
        {
            if (init)
            {
                if ((mapPixelX >= 0) && (mapPixelX < WoodsFile.mapWidthValue) && (mapPixelY >= 0) && (mapPixelY < WoodsFile.mapHeightValue))
                {
                    return (mapMultipliers[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]);
                }
                else
                {
                    return (1.0f);
                }
            }
            else
            {
                return (1.0f);
            }
        }

        private static float GetAdditionalHeightBasedOnClimate(int mapPixelX, int mapPixelY)
        {
            int worldClimate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mapPixelX, mapPixelY);
            float additionalHeightBasedOnClimate = 0.0f;
            switch (worldClimate)
            {
                case (int)MapsFile.Climates.Ocean:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateOcean;
                    break;
                case (int)MapsFile.Climates.Desert:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateDesert;
                    break;
                case (int)MapsFile.Climates.Desert2:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateDesert2;
                    break;
                case (int)MapsFile.Climates.Mountain:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateMountain;
                    break;
                case (int)MapsFile.Climates.Rainforest:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateRainforest;
                    break;
                case (int)MapsFile.Climates.Swamp:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateSwamp;
                    break;
                case (int)MapsFile.Climates.Subtropical:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateSubtropical;
                    break;
                case (int)MapsFile.Climates.MountainWoods:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateMountainWoods;
                    break;
                case (int)MapsFile.Climates.Woodlands:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateWoodlands;
                    break;
                case (int)MapsFile.Climates.HauntedWoodlands:
                    additionalHeightBasedOnClimate = additionalHeightNoiseClimateHauntedWoodlands;
                    break;
            }
            return additionalHeightBasedOnClimate;
        }

        /// <summary>
        /// initializes resources (mapDistanceSquaredFromWater, mapDistanceSquaredFromLocations, mapMultipliers) and smoothes small height map
        /// </summary>
        public static void InitImprovedWorldTerrain(ContentReader contentReader)
        {
            ___InitImprovedWorldTerrain.Begin();

            if (!init)
            {
                #if CREATE_PERSISTENT_LOCATION_RANGE_MAPS
                {
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;

                    mapLocationRangeX = new byte[width * height];
                    mapLocationRangeY = new byte[width * height];

                    //int y = 204;
                    //int x = 718;
                    var mapFileReader = contentReader.MapFileReader;
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        //MapPixelData MapData = TerrainHelper.GetMapPixelData(contentReader, x, y);
                        //if (MapData.hasLocation)
                        //{
                        //    int locationRangeX = (int)MapData.locationRect.xMax - (int)MapData.locationRect.xMin;
                        //    int locationRangeY = (int)MapData.locationRect.yMax - (int)MapData.locationRect.yMin;
                        //}

                        ContentReader.MapSummary mapSummary;
                        int regionIndex = -1, mapIndex = -1;
                        bool hasLocation = contentReader.HasLocation(x, y, out mapSummary);
                        if (hasLocation)
                        {   
                            regionIndex = mapSummary.RegionIndex;
                            mapIndex = mapSummary.MapIndex;
                            DFLocation location = mapFileReader.GetLocation(regionIndex, mapIndex);
                            byte locationRangeX = location.Exterior.ExteriorData.Width;
                            byte locationRangeY = location.Exterior.ExteriorData.Height;

                            mapLocationRangeX[y * width + x] = locationRangeX;
                            mapLocationRangeY[y * width + x] = locationRangeY;
                        }
                    }
              
                    // save to files
                    FileStream ostream;
                    ostream = new FileStream(Path.Combine(Application.dataPath, out_filepathMapLocationRangeX), FileMode.Create, FileAccess.Write);
                    BinaryWriter writerMapLocationRangeX = new BinaryWriter(ostream, Encoding.UTF8);
                    writerMapLocationRangeX.Write(mapLocationRangeX, 0, width * height);
                    writerMapLocationRangeX.Close();
                    ostream.Close();

                    ostream = new FileStream(Path.Combine(Application.dataPath, out_filepathMapLocationRangeY), FileMode.Create, FileAccess.Write);
                    BinaryWriter writerMapLocationRangeY = new BinaryWriter(ostream, Encoding.UTF8);
                    writerMapLocationRangeY.Write(mapLocationRangeY, 0, width * height);
                    writerMapLocationRangeY.Close();
                    ostream.Close();
                }
                #else
                {
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;

                    mapLocationRangeX = new byte[width * height];
                    mapLocationRangeY = new byte[width * height];

                    IO.MemoryStream istream;
                    TextAsset assetMapLocationRangeX = Resources.Load<TextAsset>(filenameMapLocationRangeX);
                    if (assetMapLocationRangeX != null)
                    {
                        istream = new IO.MemoryStream(assetMapLocationRangeX.bytes);
                        IO.BinaryReader readerMapLocationRangeX = new IO.BinaryReader(istream, Text.Encoding.UTF8);
                        readerMapLocationRangeX.Read(mapLocationRangeX, 0, width * height);
                        readerMapLocationRangeX.Close();
                        istream.Close();
                    }

                    TextAsset assetMapLocationRangeY = Resources.Load<TextAsset>(filenameMapLocationRangeY);
                    if (assetMapLocationRangeY)
                    {
                        istream = new IO.MemoryStream(assetMapLocationRangeY.bytes);
                        IO.BinaryReader readerMapLocationRangeY = new IO.BinaryReader(istream, Text.Encoding.UTF8);
                        readerMapLocationRangeY.Read(mapLocationRangeY, 0, width * height);
                        readerMapLocationRangeY.Close();
                        istream.Close();
                    }

                    //FileStream istream;
                    //istream = new FileStream(filepathMapLocationRangeX, FileMode.Open, FileAccess.Read);
                    //BinaryReader readerMapLocationRangeX = new BinaryReader(istream, Encoding.UTF8);
                    //readerMapLocationRangeX.Read(mapLocationRangeX, 0, width * height);
                    //readerMapLocationRangeX.Close();
                    //istream.Close();

                    //istream = new FileStream(filepathMapLocationRangeY, FileMode.Open, FileAccess.Read);
                    //BinaryReader readerMapLocationRangeY = new BinaryReader(istream, Encoding.UTF8);
                    //readerMapLocationRangeY.Read(mapLocationRangeY, 0, width * height);
                    //readerMapLocationRangeY.Close();
                    //istream.Close();
                }
                #endif

                if (mapDistanceSquaredFromWater == null)
                {
                    byte[] heightMapArray = contentReader.WoodsFileReader.Buffer.Clone() as byte[];
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        heightMapArray[i] = heightMapArray[i] <= 2 ? (byte)1 : (byte)0;
                    }

                    //now set image borders to "water" (this is a workaround to prevent mountains to become too high in north-east and south-east edge of map)
                    for (int y = 0; y < height; y++)
                    {
                        heightMapArray[y * width + 0] = 1;
                        heightMapArray[y * width + width - 1] = 1;
                    }
                    for (int x = 0; x < width; x++)
                    {
                        heightMapArray[0 * width + x] = 1;
                        heightMapArray[(height - 1) * width + x] = 1;
                    }

                    mapDistanceSquaredFromWater = ImageDistanceTransform(heightMapArray, width, height, 1);

                    heightMapArray = null;
                }

                if (mapDistanceSquaredFromLocations == null)
                {
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;
                    mapLocations = new byte[width * height];

                    ContentReader.MapSummary summary = default(ContentReader.MapSummary);
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        mapLocations[y * width + x] = contentReader.HasLocation(x + 1, y+1, out summary) ? (byte)1 : (byte)0;
                    }
                    mapDistanceSquaredFromLocations = ImageDistanceTransform(mapLocations, width, height, 1);
                }                

                if (mapMultipliers == null)
                {
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;
                    mapMultipliers = new float[width * height];

                    // compute the multiplier and store it in mapMultipliers
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float distanceFromWater = (float)math.sqrt(mapDistanceSquaredFromWater[y * width + x]);
                        float distanceFromLocation = (float)math.sqrt(mapDistanceSquaredFromLocations[y * width + x]);
                        float multiplierLocation = (distanceFromLocation * extraExaggerationFactorLocationDistance + 1.0f); // terrain distant from location gets extra exaggeration
                        if (distanceFromWater < minDistanceFromWaterForExtraExaggeration) // except if it is near water
                            multiplierLocation = 1.0f;

                        // Seed random with terrain key
                        UnityEngine.Random.InitState(TerrainHelper.MakeTerrainKey(x, y));

                        float additionalHeightBasedOnClimate = GetAdditionalHeightBasedOnClimate(x, y);
                        float additionalHeightApplied = UnityEngine.Random.Range(-additionalHeightBasedOnClimate * 0.5f, additionalHeightBasedOnClimate);
                        mapMultipliers[y * width + x] = (math.min(maxHeightsExaggerationMultiplier, additionalHeightApplied + /*multiplierLocation **/ math.max(1.0f, distanceFromWater * exaggerationFactorWaterDistance)));
                    }

                    // multipliedMap gets smoothed
                    float[] newmapMultipliers = mapMultipliers.Clone() as float[];
                    float[,] weights = { { 0.0625f, 0.125f, 0.0625f }, { 0.125f, 0.25f, 0.125f }, { 0.0625f, 0.125f, 0.0625f } };
                    for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                    {
                        int i = y * width + x;
                        if (mapDistanceSquaredFromLocations[i] <= 2) // at and around locations ( <= 2 ... only map pixels in 8-connected neighborhood (distanceFromLocationMaps stores squared distances...))
                        {
                            newmapMultipliers[i] =
                                weights[0, 0] * mapMultipliers[(y - 1) * width + (x - 1)] + weights[0, 1] * mapMultipliers[(y - 1) * width + (x)] + weights[0, 2] * mapMultipliers[(y - 1) * width + (x + 1)] +
                                weights[1, 0] * mapMultipliers[(y - 0) * width + (x - 1)] + weights[1, 1] * mapMultipliers[(y - 0) * width + (x)] + weights[1, 2] * mapMultipliers[(y - 0) * width + (x + 1)] +
                                weights[2, 0] * mapMultipliers[(y + 1) * width + (x - 1)] + weights[2, 1] * mapMultipliers[(y + 1) * width + (x)] + weights[2, 2] * mapMultipliers[(y + 1) * width + (x + 1)];
                        }
                    }
                    mapMultipliers = newmapMultipliers;

                    newmapMultipliers = null;
                    weights = null;
                }

                //the height map gets smoothed as well
                {
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;
                    byte[] heightMapBuffer = contentReader.WoodsFileReader.Buffer.Clone() as byte[];
                    int[,] intWeights = { { 1, 2, 1 }, { 2, 4, 2 }, { 1, 2, 1 } };
                    for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (mapDistanceSquaredFromWater[y * width + x] > 0) // check if squared distance from water is greater than zero -> if it is no water pixel
                        {
                            int value =
                                intWeights[0, 0] * (int)heightMapBuffer[(y - 1) * width + (x - 1)] + intWeights[0, 1] * (int)heightMapBuffer[(y - 1) * width + (x)] + intWeights[0, 2] * (int)heightMapBuffer[(y - 1) * width + (x + 1)] +
                                intWeights[1, 0] * (int)heightMapBuffer[(y - 0) * width + (x - 1)] + intWeights[1, 1] * (int)heightMapBuffer[(y - 0) * width + (x)] + intWeights[1, 2] * (int)heightMapBuffer[(y - 0) * width + (x + 1)] +
                                intWeights[2, 0] * (int)heightMapBuffer[(y + 1) * width + (x - 1)] + intWeights[2, 1] * (int)heightMapBuffer[(y + 1) * width + (x)] + intWeights[2, 2] * (int)heightMapBuffer[(y + 1) * width + (x + 1)];

                            heightMapBuffer[y * width + x] = (byte)(value / 16);
                        }
                    }
                    contentReader.WoodsFileReader.Buffer = heightMapBuffer;

                    heightMapBuffer = null;
                    intWeights = null;
                }

                // build tree coverage map
                if (mapTreeCoverage == null)
                {
                    int width = WoodsFile.mapWidthValue;
                    int height = WoodsFile.mapHeightValue;
                    mapTreeCoverage = new byte[width * height];

                    #if !LOAD_TREE_COVERAGE_MAP
                    {
                        float startTreeCoverageAtElevation = ImprovedTerrainSampler.baseHeightScale * 2.0f; // ImprovedTerrainSampler.scaledBeachElevation;
                        float minTreeCoverageSaturated = ImprovedTerrainSampler.baseHeightScale * 6.0f;
                        float maxTreeCoverageSaturated = ImprovedTerrainSampler.baseHeightScale * 60.0f;
                        float endTreeCoverageAtElevation = ImprovedTerrainSampler.baseHeightScale * 80.0f;
                        //float maxElevation = 0.0f;
                        var woodsFileReaderBuffer = contentReader.WoodsFileReader.Buffer;
                        for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int readIndex = (height - 1 - y) * width + x;
                            float w = 0.0f;

                            //float elevation = ((float)woodsFileReaderBuffer[(height - 1 - y) * width + x]) / 255.0f; // *mapMultipliers[index];
                            float elevation = ((float)woodsFileReaderBuffer[readIndex]) * mapMultipliers[readIndex];

                            //maxElevation = math.max(maxElevation, elevation);
                            if ((elevation > minTreeCoverageSaturated) && (elevation < maxTreeCoverageSaturated))
                            {
                                w = 1.0f;
                            }
                            else if ((elevation >= startTreeCoverageAtElevation) && (elevation <= minTreeCoverageSaturated))
                            {
                                w = (elevation - startTreeCoverageAtElevation) / (minTreeCoverageSaturated - startTreeCoverageAtElevation);
                            }
                            else if ((elevation >= maxTreeCoverageSaturated) && (elevation <= endTreeCoverageAtElevation))
                            {
                                w = 1.0f - ((elevation - maxTreeCoverageSaturated) / (endTreeCoverageAtElevation - maxTreeCoverageSaturated));
                            }

                            //w = 0.65f * w + 0.35f * math.min(6.0f, (float)math.sqrt(mapDistanceSquaredFromLocations[y * width + x])) / 6.0f;

                            mapTreeCoverage[y * width + x] = Convert.ToByte(w * 255.0f);

                            //if (elevation>0.05f)
                            //    mapTreeCoverage[index] = Convert.ToByte(250); //w * 255.0f);
                            //else mapTreeCoverage[index] = Convert.ToByte(0);

                            //if (elevation >= startTreeCoverageAtElevation)
                            //{
                            //    mapTreeCoverage[(y) * width + x] = Convert.ToByte(255.0f);
                            //} else{
                            //    mapTreeCoverage[(y) * width + x] = Convert.ToByte(0.0f);
                            //}
                        }
                    }
                    #else
                    {
                        TextAsset assetMapTreeCoverage = Resources.Load<TextAsset>(filenameTreeCoverageMap);
                        if (assetMapTreeCoverage)
                        {
                            IO.MemoryStream istream = new IO.MemoryStream(assetMapTreeCoverage.bytes);
                            IO.BinaryReader readerMapTreeCoverage = new IO.BinaryReader(istream, Text.Encoding.UTF8);
                            readerMapTreeCoverage.Read(mapTreeCoverage, 0, width * height);
                            readerMapTreeCoverage.Close();
                            istream.Close();
                        }
                    }
                    #endif

                    #if CREATE_PERSISTENT_TREE_COVERAGE_MAP
                    {
                        FileStream ostream = new FileStream(Path.Combine(Application.dataPath, out_filepathOutTreeCoverageMap), FileMode.Create, FileAccess.Write);
                        BinaryWriter writerMapTreeCoverage = new BinaryWriter(ostream, Encoding.UTF8);
                        writerMapTreeCoverage.Write(mapTreeCoverage, 0, width * height);
                        writerMapTreeCoverage.Close();
                        ostream.Close();
                    }
                    #endif
                    //Debug.Log(string.Format("max elevation: {0}", maxElevation));
                }
                
                init = true;
            }

            ___InitImprovedWorldTerrain.End();
        }

        /* distance transform of image (will get binarized) using squared distance */
        public static float[] ImageDistanceTransform(byte[] imgIn, int width, int height, byte maskValue)
        {
            ___ImageDistanceTransform.Begin();

            // allocate image and initialize
            float[] imgOut = new float[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                imgOut[i] = imgIn[i] == maskValue
                    ? 0 // pixels with maskValue -> distance 0
                    : 1E20f; // set to infinite
            }

            DistanceTransform2D(imgOut, width, height);
            
            ___ImageDistanceTransform.End();
            return imgOut;
        }

        /* euklidean distance transform (based on an implementation of Pedro Felzenszwalb (from paper Fast distance transform in C++ by Felzenszwalb and Huttenlocher))*/

        /* distance transform of 1d function using squared distance */
        private static float[] DistanceTransform1D(float[] f, int n)
        {
            ___DistanceTransform1D.Begin();

            const float INF = 1E20f;

            float[] d = new float[n];
            int[] v = new int[n];
            float[] z = new float[n + 1];
            int k = 0;
            v[0] = 0;
            z[0] = -INF;
            z[1] = +INF;
            for (int q = 1; q <= n - 1; q++)
            {
                float s = ((f[q] + (q * q)) - (f[v[k]] + (v[k] * v[k]))) / (2 * q - 2 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + (q * q)) - (f[v[k]] + (v[k] * v[k]))) / (2 * q - 2 * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = +INF;
            }

            k = 0;
            for (int q = 0; q <= n - 1; q++)
            {
                while (z[k + 1] < q)
                    k++;
                d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
            }

            ___DistanceTransform1D.End();
            return d;
        }

        /* in-place 2D distance transform on float array using squared distance, float array must be initialized with 0 for maskValue-pixels and infinite otherwise*/
        private static void DistanceTransform2D(float[] img, int width, int height)
        {
            ___DistanceTransform2D.Begin();

            float[] f = new float[math.max(width, height)];

            // transform along columns
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    f[y] = img[y * width + x];
                }
                float[] d = DistanceTransform1D(f, height);
                for (int y = 0; y < height; y++)
                {
                    img[y * width + x] = d[y];
                }
            }

            // transform along rows
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    f[x] = img[y * width + x];
                }
                float[] d = DistanceTransform1D(f, width);
                for (int x = 0; x < width; x++)
                {
                    img[y * width + x] = d[x];
                }
            }

            ___DistanceTransform2D.End();
        }

    }
}
