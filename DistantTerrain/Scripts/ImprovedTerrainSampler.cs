//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;

//namespace DaggerfallWorkshop
namespace DistantTerrain
{
    /// <summary>
    /// Default TerrainSampler for StreamingWorld.
    /// </summary>
    public class ImprovedTerrainSampler : TerrainSampler
    {
        public const float
        // Scale factors for this sampler implementation
            baseHeightScale = 8f, //12f; //16f; // 8f;        
            defaultNoiseMapScale = 15f,
            defaultExtraNoiseScale = 3f,
        // additional height noise based on climate      
            noiseMapScaleClimateOcean = 0.0f,
            noiseMapScaleClimateDesert = 2.0f, //1.25f;
            noiseMapScaleClimateDesert2 = 14.5f,
            noiseMapScaleClimateMountain = 15.0f,
            noiseMapScaleClimateRainforest = 11.5f,
            noiseMapScaleClimateSwamp = 3.8f,
            noiseMapScaleClimateSubtropical = 3.35f, // 3.25f
            noiseMapScaleClimateMountainWoods = 16.5f, // 12.5f
            noiseMapScaleClimateWoodlands = 18.0f, //10.0f;
            noiseMapScaleClimateHauntedWoodlands = 8.0f,
            maxNoiseMapScale = 32.0f, //32f; //15f; //4f; //15f; //4f;
        // extra noise scale based on climate
            extraNoiseScaleClimateOcean = 0.0f,
            extraNoiseScaleClimateDesert = 29f, //7f;
            extraNoiseScaleClimateDesert2 = 38f,
            extraNoiseScaleClimateMountain = 62f,
            extraNoiseScaleClimateRainforest = 16f,
            extraNoiseScaleClimateSwamp = 20f,
            extraNoiseScaleClimateSubtropical = 26f, // 17f
            extraNoiseScaleClimateMountainWoods = 32f,
            extraNoiseScaleClimateWoodlands = 24f,
            extraNoiseScaleClimateHauntedWoodlands = 22f,
            interpolationEndDistanceFromWaterForNoiseScaleMultiplier = 5.0f,
        //    extraNoiseScale = 3f; //10f; //3f;
            scaledOceanElevation = 3.4f * baseHeightScale,
            scaledBeachElevation = 5.0f * baseHeightScale,

        // Max terrain height of this sampler implementation
            maxTerrainHeight = ImprovedWorldTerrain.maxHeightsExaggerationMultiplier * baseHeightScale * 128 + maxNoiseMapScale * 128 + 128;//1380f; //26115f;

        public override int Version
        {
            get { return 3; }
        }

        public ImprovedTerrainSampler()
        {
            HeightmapDimension = defaultHeightmapDimension;
            MaxTerrainHeight = maxTerrainHeight;
            OceanElevation = scaledOceanElevation;
            BeachElevation = scaledBeachElevation;
        }

        public override float TerrainHeightScale(int x, int y)
        {
            return ImprovedWorldTerrain.computeHeightMultiplier(x, y) * ImprovedTerrainSampler.baseHeightScale + this.GetNoiseMapScaleBasedOnClimate(x, y); // * 0.5f;
        }


        private float GetNoiseMapScaleBasedOnClimate(int mapPixelX, int mapPixelY)
        {
            int worldClimate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mapPixelX, mapPixelY);
            float noiseMapScaleClimate = defaultNoiseMapScale;
            switch (worldClimate)
            {
                case (int)MapsFile.Climates.Ocean:
                    noiseMapScaleClimate = noiseMapScaleClimateOcean;
                    break;
                case (int)MapsFile.Climates.Desert:
                    noiseMapScaleClimate = noiseMapScaleClimateDesert;
                    break;
                case (int)MapsFile.Climates.Desert2:
                    noiseMapScaleClimate = noiseMapScaleClimateDesert2;
                    break;
                case (int)MapsFile.Climates.Mountain:
                    noiseMapScaleClimate = noiseMapScaleClimateMountain;
                    break;
                case (int)MapsFile.Climates.Rainforest:
                    noiseMapScaleClimate = noiseMapScaleClimateRainforest;
                    break;
                case (int)MapsFile.Climates.Swamp:
                    noiseMapScaleClimate = noiseMapScaleClimateSwamp;
                    break;
                case (int)MapsFile.Climates.Subtropical:
                    noiseMapScaleClimate = noiseMapScaleClimateSubtropical;
                    break;
                case (int)MapsFile.Climates.MountainWoods:
                    noiseMapScaleClimate = noiseMapScaleClimateMountainWoods;
                    break;
                case (int)MapsFile.Climates.Woodlands:
                    noiseMapScaleClimate = noiseMapScaleClimateWoodlands;
                    break;
                case (int)MapsFile.Climates.HauntedWoodlands:
                    noiseMapScaleClimate = noiseMapScaleClimateHauntedWoodlands;
                    break;
            }
            return noiseMapScaleClimate;
        }

        ///// <summary>
        ///// compensates for different extra noise scale values based on different climate in neighboring map pixels
        ///// note: this does only work if only 2 different climates occur in the 8-neighborhoods of both involved pixel
        ///// </summary>
        ///// <param name="mapPixelX"></param>
        ///// <param name="mapPixelY"></param>
        ///// <returns></returns>
        //private float GetExtraNoiseScaleBasedOnClimate(int mapPixelX, int mapPixelY)
        //{
        //    int worldClimate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mapPixelX, mapPixelY);
        //    int worldClimate2 = worldClimate;
        //    for (int y=-1; y <= 1; y++)
        //    {
        //        for (int x = -1; x <= 1; x++)
        //        {
        //            int mX = math.max(0, math.min(mapPixelX + x, WoodsFile.mapWidthValue - 1));
        //            int mY = math.max(0, math.min(mapPixelY + y, WoodsFile.mapHeightValue - 1));
        //            int climate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mX, mY);
        //            if (climate != worldClimate)
        //            {
        //                worldClimate2 = climate;
        //            }
        //        }
        //    }
        //    if (worldClimate == worldClimate2)
        //        return (GetExtraNoiseScaleBasedOnClimate(worldClimate));
        //    else
        //        return((GetExtraNoiseScaleBasedOnClimate(worldClimate) + GetExtraNoiseScaleBasedOnClimate(worldClimate2))/2.0f);
        //}

        //private float GetExtraNoiseScaleBasedOnClimate(int worldClimate)
        //{
        //    float extraNoiseScale = defaultExtraNoiseScale;
        //    switch (worldClimate)
        //    {
        //        case (int)MapsFile.Climates.Ocean:
        //            extraNoiseScale = extraNoiseScaleClimateOcean;
        //            break;
        //        case (int)MapsFile.Climates.Desert:
        //            extraNoiseScale = extraNoiseScaleClimateDesert;
        //            break;
        //        case (int)MapsFile.Climates.Desert2:
        //            extraNoiseScale = extraNoiseScaleClimateDesert2;
        //            break;
        //        case (int)MapsFile.Climates.Mountain:
        //            extraNoiseScale = extraNoiseScaleClimateMountain;
        //            break;
        //        case (int)MapsFile.Climates.Rainforest:
        //            extraNoiseScale = extraNoiseScaleClimateRainforest;
        //            break;
        //        case (int)MapsFile.Climates.Swamp:
        //            extraNoiseScale = extraNoiseScaleClimateSwamp;
        //            break;
        //        case (int)MapsFile.Climates.Subtropical:
        //            extraNoiseScale = extraNoiseScaleClimateSubtropical;
        //            break;
        //        case (int)MapsFile.Climates.MountainWoods:
        //            extraNoiseScale = extraNoiseScaleClimateMountainWoods;
        //            break;
        //        case (int)MapsFile.Climates.Woodlands:
        //            extraNoiseScale = extraNoiseScaleClimateWoodlands;
        //            break;
        //        case (int)MapsFile.Climates.HauntedWoodlands:
        //            extraNoiseScale = extraNoiseScaleClimateHauntedWoodlands;
        //            break;
        //    }
        //    return extraNoiseScale;
        //}

        private float GetExtraNoiseScaleBasedOnClimate(int mapPixelX, int mapPixelY)
        {
            // small terrain features' height should depend on climate of map pixel
            float extraNoiseScale = defaultExtraNoiseScale;
            int worldClimate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mapPixelX, mapPixelY);

            switch (worldClimate)
            {
                case (int)MapsFile.Climates.Ocean:
                    extraNoiseScale = extraNoiseScaleClimateOcean;
                    break;
                case (int)MapsFile.Climates.Desert:
                    extraNoiseScale = extraNoiseScaleClimateDesert;
                    break;
                case (int)MapsFile.Climates.Desert2:
                    extraNoiseScale = extraNoiseScaleClimateDesert2;
                    break;
                case (int)MapsFile.Climates.Mountain:
                    extraNoiseScale = extraNoiseScaleClimateMountain;
                    break;
                case (int)MapsFile.Climates.Rainforest:
                    extraNoiseScale = extraNoiseScaleClimateRainforest;
                    break;
                case (int)MapsFile.Climates.Swamp:
                    extraNoiseScale = extraNoiseScaleClimateSwamp;
                    break;
                case (int)MapsFile.Climates.Subtropical:
                    extraNoiseScale = extraNoiseScaleClimateSubtropical;
                    break;
                case (int)MapsFile.Climates.MountainWoods:
                    extraNoiseScale = extraNoiseScaleClimateMountainWoods;
                    break;
                case (int)MapsFile.Climates.Woodlands:
                    extraNoiseScale = extraNoiseScaleClimateWoodlands;
                    break;
                case (int)MapsFile.Climates.HauntedWoodlands:
                    extraNoiseScale = extraNoiseScaleClimateHauntedWoodlands;
                    break;
            }
            return extraNoiseScale;
        }


        ///// <summary>
        ///// helper class to do parallel computing of heights
        ///// </summary>
        //public class HeightsComputationTask
        //{
        //    public class DataForTask
        //    {
        //        public int numTasks;
        //        public int currentTask;
        //        public int HeightmapDimension;
        //        public float MaxTerrainHeight;
        //        public float div;
        //        public float[,] baseHeightValue;
        //        public byte[,] lhm;
        //        public float[,] noiseHeightMultiplierMap;
        //        //public float extraNoiseScaleBasedOnClimate;
        //        public float extraNoiseScaleBasedOnClimateTopLeft;
        //        public float extraNoiseScaleBasedOnClimateTopRight;
        //        public float extraNoiseScaleBasedOnClimateBottomLeft;
        //        public float extraNoiseScaleBasedOnClimateBottomRight;
        //        public MapPixelData mapPixel;
        //    }

        //    private ManualResetEvent _doneEvent;

        //    public HeightsComputationTask(ManualResetEvent doneEvent)
        //    {
        //        _doneEvent = doneEvent;
        //    }

        //    public void ThreadProc(System.Object stateInfo)
        //    {
        //        DataForTask dataForTask = stateInfo as DataForTask;

        //        // Extract height samples for all chunks
        //        float baseHeight, noiseHeight;
        //        float x1, x2, x3, x4;

        //        int dim = dataForTask.HeightmapDimension;
        //        float div = dataForTask.div;
        //        float[,] baseHeightValue = dataForTask.baseHeightValue;
        //        byte[,] lhm = dataForTask.lhm;
        //        float[,] noiseHeightMultiplierMap = dataForTask.noiseHeightMultiplierMap;
        //        //float extraNoiseScaleBasedOnClimate = dataForTask.extraNoiseScaleBasedOnClimate;
        //        float extraNoiseScaleBasedOnClimateTopLeft = dataForTask.extraNoiseScaleBasedOnClimateTopLeft;
        //        float extraNoiseScaleBasedOnClimateTopRight = dataForTask.extraNoiseScaleBasedOnClimateTopRight;
        //        float extraNoiseScaleBasedOnClimateBottomLeft = dataForTask.extraNoiseScaleBasedOnClimateBottomLeft;
        //        float extraNoiseScaleBasedOnClimateBottomRight = dataForTask.extraNoiseScaleBasedOnClimateBottomRight;

        //        // split the work between different tasks running in different threads (thread n computes data elements n, n + numTasks, n + numTasks*2, ...)
        //        for (int y = dataForTask.currentTask; y < dim; y+=dataForTask.numTasks)
        //        {
        //            for (int x = 0; x < dim; x++)
        //            {
        //                float rx = (float)x / div;
        //                float ry = (float)y / div;
        //                int ix = (int)math.floor(rx);
        //                int iy = (int)math.floor(ry);
        //                float sfracx = (float)x / (float)(dim - 1);
        //                float sfracy = (float)y / (float)(dim - 1);
        //                float fracx = (float)(x - ix * div) / div;
        //                float fracy = (float)(y - iy * div) / div;
        //                float scaledHeight = 0;

        //                // Bicubic sample small height map for base terrain elevation
        //                x1 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 3], baseHeightValue[1, 3], baseHeightValue[2, 3], baseHeightValue[3, 3], sfracx);
        //                x2 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 2], baseHeightValue[1, 2], baseHeightValue[2, 2], baseHeightValue[3, 2], sfracx);
        //                x3 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 1], baseHeightValue[1, 1], baseHeightValue[2, 1], baseHeightValue[3, 1], sfracx);
        //                x4 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 0], baseHeightValue[1, 0], baseHeightValue[2, 0], baseHeightValue[3, 0], sfracx);
        //                baseHeight = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, sfracy);
        //                scaledHeight += baseHeight * baseHeightScale;

        //                // Bicubic sample large height map for noise mask over terrain features
        //                x1 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 0], lhm[ix + 1, iy + 0], lhm[ix + 2, iy + 0], lhm[ix + 3, iy + 0], fracx);
        //                x2 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 1], lhm[ix + 1, iy + 1], lhm[ix + 2, iy + 1], lhm[ix + 3, iy + 1], fracx);
        //                x3 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 2], lhm[ix + 1, iy + 2], lhm[ix + 2, iy + 2], lhm[ix + 3, iy + 2], fracx);
        //                x4 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 3], lhm[ix + 1, iy + 3], lhm[ix + 2, iy + 3], lhm[ix + 3, iy + 3], fracx);
        //                noiseHeight = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, fracy);

        //                x1 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 3], noiseHeightMultiplierMap[1, 3], noiseHeightMultiplierMap[2, 3], noiseHeightMultiplierMap[3, 3], sfracx);
        //                x2 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 2], noiseHeightMultiplierMap[1, 2], noiseHeightMultiplierMap[2, 2], noiseHeightMultiplierMap[3, 2], sfracx);
        //                x3 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 1], noiseHeightMultiplierMap[1, 1], noiseHeightMultiplierMap[2, 1], noiseHeightMultiplierMap[3, 1], sfracx);
        //                x4 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 0], noiseHeightMultiplierMap[1, 0], noiseHeightMultiplierMap[2, 0], noiseHeightMultiplierMap[3, 0], sfracx);
        //                float noiseHeightMultiplier = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, sfracy);

        //                scaledHeight += noiseHeight * noiseHeightMultiplier;

        //                // Additional noise mask for small terrain features at ground level
        //                // small terrain features' height scale should depend on climate of map pixel
        //                float extraNoiseScaleTopLeft = extraNoiseScaleBasedOnClimateTopLeft;
        //                float extraNoiseScaleTopRight = extraNoiseScaleBasedOnClimateTopRight;
        //                float extraNoiseScaleBottomLeft = extraNoiseScaleBasedOnClimateBottomLeft;
        //                float extraNoiseScaleBottomRight = extraNoiseScaleBasedOnClimateBottomRight;
        //                //float extraNoiseScale = (1.0f - sfracx) * (1.0f - sfracy) * extraNoiseScaleTopLeft +
        //                //                        (sfracx) *(1.0f - sfracy) * extraNoiseScaleTopRight +
        //                //                        (1.0f - sfracx) * (sfracy) * extraNoiseScaleBottomLeft +
        //                //                        (sfracx) * (sfracx) * extraNoiseScaleBottomRight;
        //                float extraNoiseScale = TerrainHelper.BilinearInterpolator(extraNoiseScaleTopLeft, extraNoiseScaleBottomLeft, extraNoiseScaleTopRight, extraNoiseScaleBasedOnClimateBottomRight, sfracx, sfracy);
        //                //float extraNoiseScale = TerrainHelper.BilinearInterpolator(extraNoiseScaleBottomLeft, extraNoiseScaleTopLeft, extraNoiseScaleBasedOnClimateBottomRight, extraNoiseScaleTopRight , sfracx, sfracy);

        //                //float extraNoiseScale = extraNoiseScaleBasedOnClimate;
        //                //// prevent seams between different climate map pixels
        //                //if (x <= 0 || y <= 0 || x >= hDim - 1 || y >= hDim - 1)
        //                //{
        //                //    extraNoiseScale = defaultExtraNoiseScale;
        //                //}
        //                //float extraNoiseScale = extraNoiseScaleBasedOnClimate;
        //                ////// prevent seams between different climate map pixels
        //                ////if (x <= 0 || y <= 0 || x >= dim - 1 || y >= dim - 1)
        //                ////{
        //                ////    extraNoiseScale = defaultExtraNoiseScale;
        //                ////}
        //                int noisex = dataForTask.mapPixel.mapPixelX * (dataForTask.HeightmapDimension - 1) + x;
        //                int noisey = (MapsFile.MaxMapPixelY - dataForTask.mapPixel.mapPixelY) * (dataForTask.HeightmapDimension - 1) + y;
        //                float lowFreq = TerrainHelper.GetNoise(noisex, noisey, 0.3f, 0.5f, 0.5f, 1);
        //                float highFreq = TerrainHelper.GetNoise(noisex, noisey, 0.9f, 0.5f, 0.5f, 1);
        //                scaledHeight += (lowFreq * highFreq) * extraNoiseScale;

        //                // Clamp lower values to ocean elevation
        //                if (scaledHeight < scaledOceanElevation)
        //                    scaledHeight = scaledOceanElevation;

        //                // Set sample
        //                float height = math.saturate(scaledHeight / dataForTask.MaxTerrainHeight);
        //                dataForTask.mapPixel.heightmapSamples[y, x] = height;
        //            }
        //        }

        //        _doneEvent.Set();
        //    }
        //}

        public override void GenerateSamples(ref MapPixelData mapPixel)
        {
            ////System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ////long startTime = stopwatch.ElapsedMilliseconds;

            //DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

            //// Create samples arrays
            //mapPixel.heightmapSamples = new float[HeightmapDimension, HeightmapDimension];

            //// Divisor ensures continuous 0-1 range of tile samples
            //float div = (float)(HeightmapDimension - 1) / 3f;

            //// Read neighbouring height samples for this map pixel
            //int mx = mapPixel.mapPixelX;
            //int my = mapPixel.mapPixelY;

            //// Seed random with terrain key
            //UnityEngine.Random.InitState(TerrainHelper.MakeTerrainKey(mx, my));

            //byte[,] shm = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValuesRange(mx - 2, my - 2, 4);
            //byte[,] lhm = dfUnity.ContentReader.WoodsFileReader.GetLargeHeightMapValuesRange(mx - 1, my, 3);

            //float[,] baseHeightValue = new float[4, 4];
            //for (int y = 0; y < 4; y++)
            //{
            //    for (int x = 0; x < 4; x++)
            //    {
            //        int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
            //        int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));

            //        baseHeightValue[x, y] = shm[x,y] * ImprovedWorldTerrain.computeHeightMultiplier(mapPixelX, mapPixelY);
            //    }
            //}

            //float[,] waterMap = new float[4, 4];
            //for (int y = 0; y < 4; y++)
            //{
            //    for (int x = 0; x < 4; x++)
            //    {
            //        if (shm[x, y] <= 2) // mappixel is water
            //            waterMap[x, y] = 0.0f;
            //        else
            //            waterMap[x, y] = 1.0f;
            //    }
            //}

            //float[,] climateMap = new float[4, 4];
            //for (int y = 0; y < 4; y++)
            //{
            //    for (int x = 0; x < 4; x++)
            //    {
            //        int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
            //        int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));                    
            //        climateMap[x, y] = GetNoiseMapScaleBasedOnClimate(mapPixelX, mapPixelY);
            //    }
            //}

            //float[,] waterDistanceMap = new float[4, 4];
            //for (int y = 0; y < 4; y++)
            //{
            //    for (int x = 0; x < 4; x++)
            //    {
            //        int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
            //        int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));
            //        waterDistanceMap[x, y] = (float)math.sqrt(ImprovedWorldTerrain.MapDistanceSquaredFromWater[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]);
            //    }
            //}

            //float[,] noiseHeightMultiplierMap = new float[4, 4];
            //for (int y = 0; y < 4; y++)
            //{
            //    for (int x = 0; x < 4; x++)
            //    {
            //        // interpolation multiplier taking near coast map pixels into account
            //        // (multiply with 0 at coast line and 1 at interpolationEndDistanceFromWaterForNoiseScaleMultiplier)
            //        float multFact = (math.min(interpolationEndDistanceFromWaterForNoiseScaleMultiplier, waterDistanceMap[x, y]) / interpolationEndDistanceFromWaterForNoiseScaleMultiplier);

            //        // blend watermap with climatemap taking into account multFact
            //        noiseHeightMultiplierMap[x, y] = waterMap[x, y] * climateMap[x, y] * multFact;
            //    }
            //}

            ////float[,] noiseHeightMultiplierMap = new float[4, 4];
            ////for (int y = 0; y < 4; y++)
            ////{
            ////    for (int x = 0; x < 4; x++)
            ////    {
            ////        int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
            ////        int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));

            ////        float climateValue = GetNoiseMapScaleBasedOnClimate(mapPixelX, mapPixelY);

            ////        float waterDistance = (float)math.sqrt(ImprovedWorldTerrain.MapDistanceSquaredFromWater[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]);

            ////        float waterValue;
            ////        if (shm[x, y] <= 2) // mappixel is water
            ////            waterValue = 0.0f;
            ////        else
            ////            waterValue = 1.0f;

            ////        // interpolation multiplier taking near coast map pixels into account
            ////        // (multiply with 0 at coast line and 1 at interpolationEndDistanceFromWaterForNoiseScaleMultiplier)
            ////        float multFact = (math.min(interpolationEndDistanceFromWaterForNoiseScaleMultiplier, waterDistance) / interpolationEndDistanceFromWaterForNoiseScaleMultiplier);

            ////        // blend watermap with climatemap taking into account multFact
            ////        noiseHeightMultiplierMap[x, y] = waterValue * climateValue * multFact;
            ////    }
            ////}

            ////int numWorkerThreads = 0, completionPortThreads = 0;
            ////int numMinWorkerThreads = 0, numMaxWorkerThreads = 0;
            ////ThreadPool.GetAvailableThreads(out numWorkerThreads, out completionPortThreads);
            ////ThreadPool.GetMinThreads(out numMinWorkerThreads, out completionPortThreads);
            ////ThreadPool.GetMaxThreads(out numMaxWorkerThreads, out completionPortThreads);
            ////Debug.Log(String.Format("available threads: {0}, numMinWorkerThreads: {1}, numMaxWorkerThreads: {2}", numWorkerThreads, numMinWorkerThreads, numMaxWorkerThreads));

            ////float extraNoiseScaleBasedOnClimate = GetExtraNoiseScaleBasedOnClimate(mx, my);
            ////float extraNoiseScaleBasedOnClimateTopLeft = (GetExtraNoiseScaleBasedOnClimate(mx - 1, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx - 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my)) / 4.0f;
            ////float extraNoiseScaleBasedOnClimateTopRight = (GetExtraNoiseScaleBasedOnClimate(mx, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my)) / 4.0f;
            ////float extraNoiseScaleBasedOnClimateBottomLeft = (GetExtraNoiseScaleBasedOnClimate(mx - 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx - 1, my + 1) + GetExtraNoiseScaleBasedOnClimate(mx, my + 1)) / 4.0f;
            ////float extraNoiseScaleBasedOnClimateBottomRight = (GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my + 1) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my + 1)) / 4.0f;
            //float extraNoiseScaleBasedOnClimateBottomLeft = (GetExtraNoiseScaleBasedOnClimate(mx - 1, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx - 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my)) / 4.0f;
            //float extraNoiseScaleBasedOnClimateBottomRight = (GetExtraNoiseScaleBasedOnClimate(mx, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my)) / 4.0f;
            //float extraNoiseScaleBasedOnClimateTopLeft = (GetExtraNoiseScaleBasedOnClimate(mx - 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx - 1, my + 1) + GetExtraNoiseScaleBasedOnClimate(mx, my + 1)) / 4.0f;
            //float extraNoiseScaleBasedOnClimateTopRight = (GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my + 1) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my + 1)) / 4.0f;

            //// the number of parallel tasks (use logical processor count for now - seems to be a good value)
            //int numParallelTasks = Environment.ProcessorCount;

            //// events used to synchronize thread computations (wait for them to finish)
            //var doneEvents = new ManualResetEvent[numParallelTasks];

            //// the array of instances of the height computations helper class
            //var heightsComputationTaskArray = new HeightsComputationTask[numParallelTasks];

            //// array of the data needed by the different tasks
            //var dataForTasks = new HeightsComputationTask.DataForTask[numParallelTasks];        

            //for (int i = 0; i < numParallelTasks; i++)
            //{
            //    doneEvents[i] = new ManualResetEvent(false);
            //    var heightsComputationTask = new HeightsComputationTask(doneEvents[i]);
            //    heightsComputationTaskArray[i] = heightsComputationTask;
            //    dataForTasks[i] = new HeightsComputationTask.DataForTask();
            //    dataForTasks[i].numTasks = numParallelTasks;
            //    dataForTasks[i].currentTask = i;
            //    dataForTasks[i].HeightmapDimension = HeightmapDimension;
            //    dataForTasks[i].MaxTerrainHeight = MaxTerrainHeight;
            //    dataForTasks[i].div = div;
            //    dataForTasks[i].baseHeightValue = baseHeightValue;
            //    dataForTasks[i].lhm = lhm;
            //    dataForTasks[i].noiseHeightMultiplierMap = noiseHeightMultiplierMap;
            //    //dataForTasks[i].extraNoiseScaleBasedOnClimate = extraNoiseScaleBasedOnClimate;
            //    dataForTasks[i].extraNoiseScaleBasedOnClimateTopLeft = extraNoiseScaleBasedOnClimateTopLeft;
            //    dataForTasks[i].extraNoiseScaleBasedOnClimateTopRight = extraNoiseScaleBasedOnClimateTopRight;
            //    dataForTasks[i].extraNoiseScaleBasedOnClimateBottomLeft = extraNoiseScaleBasedOnClimateBottomLeft;
            //    dataForTasks[i].extraNoiseScaleBasedOnClimateBottomRight = extraNoiseScaleBasedOnClimateBottomRight;
            //    dataForTasks[i].mapPixel = mapPixel;
            //    ThreadPool.QueueUserWorkItem(heightsComputationTask.ThreadProc, dataForTasks[i]);
            //}

            //// wait for all tasks to finish computation
            //WaitHandle.WaitAll(doneEvents);

            //// computed average and max height in a second pass (after threaded tasks computed all heights)
            //float averageHeight = 0;
            //float maxHeight = float.MinValue;

            //int dim = HeightmapDimension;
            //for (int y = 0; y < dim; y++)
            //{
            //    for (int x = 0; x < dim; x++)
            //    {
            //        // get sample
            //        float height = mapPixel.heightmapSamples[y, x];

            //        // Accumulate average height
            //        averageHeight += height;

            //        // Get max height
            //        if (height > maxHeight)
            //            maxHeight = height;
            //    }
            //}

            //// Average and max heights are passed back for locations
            //mapPixel.averageHeight = (averageHeight /= (float)(dim * dim));
            //mapPixel.maxHeight = maxHeight;

            ////long totalTime = stopwatch.ElapsedMilliseconds - startTime;
            ////DaggerfallUnity.LogMessage(string.Format("GenerateSamples took: {0}ms", totalTime), true);
        }


        //public override void GenerateSamplesJobs(ref MapPixelData mapPixel)
        //{
        //    throw new System.NotImplementedException();
        //}


        [Unity.Burst.BurstCompile]
        struct GenerateSamplesJob : IJobParallelFor
        {
            [ReadOnly][DeallocateOnJobCompletion] public NativeArray<float> baseHeightValue;
            [ReadOnly][DeallocateOnJobCompletion] public NativeArray<byte> lhm;
            [ReadOnly][DeallocateOnJobCompletion] public NativeArray<float> noiseHeightMultiplierMap;
            [WriteOnly] public NativeArray<float> heightmapData;
            public byte
                sd,
                ld;
            public int
                hDim,
                mapPixelX,
                mapPixelY;
            public float
                div,
                maxTerrainHeight,
                extraNoiseScaleBasedOnClimateTopLeft,
                extraNoiseScaleBasedOnClimateTopRight,
                extraNoiseScaleBasedOnClimateBottomLeft,
                extraNoiseScaleBasedOnClimateBottomRight;
            //public float extraNoiseScaleBasedOnClimate;
            public void Execute(int index)
            {
                // Use cols=x and rows=y for height data
                int
                    x = JobA.Col(index, hDim),
                    y = JobA.Row(index, hDim),
                    ix = (int)math.floor((float)x / div),
                    iy = (int)math.floor((float)y / div);
                float
                    sfracx = (float)x / (float)(hDim - 1),
                    sfracy = (float)y / (float)(hDim - 1),
                    fracx = (float)(x - ix * div) / div,
                    fracy = (float)(y - iy * div) / div,
                    scaledHeight = 0;

                int
                    i1v0 = JobA.Idx(0, 3, sd),
                    i1v1 = JobA.Idx(1, 3, sd),
                    i1v2 = JobA.Idx(2, 3, sd),
                    i1v3 = JobA.Idx(3, 3, sd),
                
                    i2v0 = JobA.Idx(0, 2, sd),
                    i2v1 = JobA.Idx(1, 2, sd),
                    i2v2 = JobA.Idx(2, 2, sd),
                    i2v3 = JobA.Idx(3, 2, sd),

                    i3v0 = JobA.Idx(0, 1, sd),
                    i3v1 = JobA.Idx(1, 1, sd),
                    i3v2 = JobA.Idx(2, 1, sd),
                    i3v3 = JobA.Idx(3, 1, sd),

                    i4v0 = JobA.Idx(0, 0, sd),
                    i4v1 = JobA.Idx(1, 0, sd),
                    i4v2 = JobA.Idx(2, 0, sd),
                    i4v3 = JobA.Idx(3, 0, sd),

                    lhmi1v0 = JobA.Idx(ix + 0, iy + 0, ld),
                    lhmi1v1 = JobA.Idx(ix + 1, iy + 0, ld),
                    lhmi1v2 = JobA.Idx(ix + 2, iy + 0, ld),
                    lhmi1v3 = JobA.Idx(ix + 3, iy + 0, ld),

                    lhmi2v0 = JobA.Idx(ix + 0, iy + 1, ld),
                    lhmi2v1 = JobA.Idx(ix + 1, iy + 1, ld),
                    lhmi2v2 = JobA.Idx(ix + 2, iy + 1, ld),
                    lhmi2v3 = JobA.Idx(ix + 3, iy + 1, ld),

                    lhmi3v0 = JobA.Idx(ix + 0, iy + 2, ld),
                    lhmi3v1 = JobA.Idx(ix + 1, iy + 2, ld),
                    lhmi3v2 = JobA.Idx(ix + 2, iy + 2, ld),
                    lhmi3v3 = JobA.Idx(ix + 3, iy + 2, ld),

                    lhmi4v0 = JobA.Idx(ix + 0, iy + 3, ld),
                    lhmi4v1 = JobA.Idx(ix + 1, iy + 3, ld),
                    lhmi4v2 = JobA.Idx(ix + 2, iy + 3, ld),
                    lhmi4v3 = JobA.Idx(ix + 3, iy + 3, ld);

                // Bicubic sample small height map for base terrain elevation
                float x1 = TerrainHelper.CubicInterpolator(baseHeightValue[i1v0], baseHeightValue[i1v1], baseHeightValue[i1v2], baseHeightValue[i1v3], sfracx);
                float x2 = TerrainHelper.CubicInterpolator(baseHeightValue[i2v0], baseHeightValue[i2v1], baseHeightValue[i2v2], baseHeightValue[i2v3], sfracx);
                float x3 = TerrainHelper.CubicInterpolator(baseHeightValue[i3v0], baseHeightValue[i3v1], baseHeightValue[i3v2], baseHeightValue[i3v3], sfracx);
                float x4 = TerrainHelper.CubicInterpolator(baseHeightValue[i4v0], baseHeightValue[i4v1], baseHeightValue[i4v2], baseHeightValue[i4v3], sfracx);
                float baseHeight = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, sfracy);
                scaledHeight += baseHeight * baseHeightScale;

                // Bicubic sample large height map for noise mask over terrain features
                x1 = TerrainHelper.CubicInterpolator(lhm[lhmi1v0], lhm[lhmi1v1], lhm[lhmi1v2], lhm[lhmi1v3], fracx);
                x2 = TerrainHelper.CubicInterpolator(lhm[lhmi2v0], lhm[lhmi2v1], lhm[lhmi2v2], lhm[lhmi2v3], fracx);
                x3 = TerrainHelper.CubicInterpolator(lhm[lhmi3v0], lhm[lhmi3v1], lhm[lhmi3v2], lhm[lhmi3v3], fracx);
                x4 = TerrainHelper.CubicInterpolator(lhm[lhmi4v0], lhm[lhmi4v1], lhm[lhmi4v2], lhm[lhmi4v3], fracx);
                float noiseHeight = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, fracy);

                x1 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[i1v0], noiseHeightMultiplierMap[i1v1], noiseHeightMultiplierMap[i1v2], noiseHeightMultiplierMap[i1v3], sfracx);
                x2 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[i2v0], noiseHeightMultiplierMap[i2v1], noiseHeightMultiplierMap[i2v2], noiseHeightMultiplierMap[i2v3], sfracx);
                x3 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[i3v0], noiseHeightMultiplierMap[i3v1], noiseHeightMultiplierMap[i3v2], noiseHeightMultiplierMap[i3v3], sfracx);
                x4 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[i4v0], noiseHeightMultiplierMap[i4v1], noiseHeightMultiplierMap[i4v2], noiseHeightMultiplierMap[i4v3], sfracx);
                float noiseHeightMultiplier = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, sfracy);
                scaledHeight += noiseHeight * noiseHeightMultiplier;

                // Additional noise mask for small terrain features at ground level
                // small terrain features' height scale should depend on climate of map pixel
                float extraNoiseScaleTopLeft = extraNoiseScaleBasedOnClimateTopLeft;
                float extraNoiseScaleTopRight = extraNoiseScaleBasedOnClimateTopRight;
                float extraNoiseScaleBottomLeft = extraNoiseScaleBasedOnClimateBottomLeft;
                float extraNoiseScaleBottomRight = extraNoiseScaleBasedOnClimateBottomRight;
                //float extraNoiseScale = (1.0f - sfracx) * (1.0f - sfracy) * extraNoiseScaleTopLeft +
                //                        (sfracx) *(1.0f - sfracy) * extraNoiseScaleTopRight +
                //                        (1.0f - sfracx) * (sfracy) * extraNoiseScaleBottomLeft +
                //                        (sfracx) * (sfracx) * extraNoiseScaleBottomRight;
                float extraNoiseScale = TerrainHelper.BilinearInterpolator(extraNoiseScaleTopLeft, extraNoiseScaleBottomLeft, extraNoiseScaleTopRight, extraNoiseScaleBasedOnClimateBottomRight, sfracx, sfracy);
                //float extraNoiseScale = TerrainHelper.BilinearInterpolator(extraNoiseScaleBottomLeft, extraNoiseScaleTopLeft, extraNoiseScaleBasedOnClimateBottomRight, extraNoiseScaleTopRight , sfracx, sfracy);
                //float extraNoiseScale = extraNoiseScaleBasedOnClimate;
                //// prevent seams between different climate map pixels
                //if (x <= 0 || y <= 0 || x >= hDim - 1 || y >= hDim - 1)
                //{
                //    extraNoiseScale = defaultExtraNoiseScale;
                //}
                int noisex = mapPixelX * (hDim - 1) + x;
                int noisey = (MapsFile.MaxMapPixelY - mapPixelY) * (hDim - 1) + y;
                float lowFreq = TerrainHelper.GetNoise(noisex, noisey, 0.3f, 0.5f, 0.5f, 1);
                float highFreq = TerrainHelper.GetNoise(noisex, noisey, 0.9f, 0.5f, 0.5f, 1);
                scaledHeight += (lowFreq * highFreq) * extraNoiseScale;

                // Clamp lower values to ocean elevation
                if (scaledHeight < scaledOceanElevation)
                    scaledHeight = scaledOceanElevation;

                // Set sample
                float height = math.saturate(scaledHeight / maxTerrainHeight);
                heightmapData[index] = height;
            }
        }

        public override JobHandle ScheduleGenerateSamplesJob(ref MapPixelData mapPixel)
        { 
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

            // Divisor ensures continuous 0-1 range of tile samples
            float div = (float)(HeightmapDimension - 1) / 3f;

            // Read neighbouring height samples for this map pixel
            int mx = mapPixel.mapPixelX;
            int my = mapPixel.mapPixelY;

            // Seed random with terrain key
            UnityEngine.Random.InitState(TerrainHelper.MakeTerrainKey(mx, my));

            byte[,] shm = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValuesRange(mx - 2, my - 2, 4);
            byte[,] lhm = dfUnity.ContentReader.WoodsFileReader.GetLargeHeightMapValuesRange(mx - 1, my, 3);

            float[,] baseHeightValue = new float[4, 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
                int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));

                baseHeightValue[x, y] = shm[x, y] * ImprovedWorldTerrain.computeHeightMultiplier(mapPixelX, mapPixelY);
            }

            float[,] waterMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                if (shm[x, y] <= 2) // mappixel is water
                    waterMap[x, y] = 0.0f;
                else
                    waterMap[x, y] = 1.0f;
            }

            float[,] climateMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
                int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));
                climateMap[x, y] = GetNoiseMapScaleBasedOnClimate(mapPixelX, mapPixelY);
            }

            float[,] waterDistanceMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                int mapPixelX = math.max(0, math.min(mx + x - 2, WoodsFile.mapWidthValue-1));
                int mapPixelY = math.max(0, math.min(my + y - 2, WoodsFile.mapHeightValue-1));
                waterDistanceMap[x, y] = (float)math.sqrt(ImprovedWorldTerrain.MapDistanceSquaredFromWater[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]);
            }

            float[,] noiseHeightMultiplierMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                // interpolation multiplier taking near coast map pixels into account
                // (multiply with 0 at coast line and 1 at interpolationEndDistanceFromWaterForNoiseScaleMultiplier)
                float multFact = (math.min(interpolationEndDistanceFromWaterForNoiseScaleMultiplier, waterDistanceMap[x, y]) / interpolationEndDistanceFromWaterForNoiseScaleMultiplier);

                // blend watermap with climatemap taking into account multFact
                noiseHeightMultiplierMap[x, y] = waterMap[x, y] * climateMap[x, y] * multFact;
            }

            //float extraNoiseScaleBasedOnClimate = GetExtraNoiseScaleBasedOnClimate(mx, my);
            //float extraNoiseScaleBasedOnClimateTopLeft = (GetExtraNoiseScaleBasedOnClimate(mx-1, my-1) + GetExtraNoiseScaleBasedOnClimate(mx, my-1)+ GetExtraNoiseScaleBasedOnClimate(mx-1, my)+ GetExtraNoiseScaleBasedOnClimate(mx,my))/4.0f;
            //float extraNoiseScaleBasedOnClimateTopRight= (GetExtraNoiseScaleBasedOnClimate(mx, my-1) + GetExtraNoiseScaleBasedOnClimate(mx+1, my-1) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx+1, my)) / 4.0f;
            //float extraNoiseScaleBasedOnClimateBottomLeft = (GetExtraNoiseScaleBasedOnClimate(mx-1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx-1, my+1) + GetExtraNoiseScaleBasedOnClimate(mx, my+1)) / 4.0f;
            //float extraNoiseScaleBasedOnClimateBottomRight = (GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx+1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my+1) + GetExtraNoiseScaleBasedOnClimate(mx+1, my+1)) / 4.0f;
            float extraNoiseScaleBasedOnClimateBottomLeft = (GetExtraNoiseScaleBasedOnClimate(mx - 1, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx - 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my)) / 4.0f;
            float extraNoiseScaleBasedOnClimateBottomRight = (GetExtraNoiseScaleBasedOnClimate(mx, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my - 1) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my)) / 4.0f;
            float extraNoiseScaleBasedOnClimateTopLeft = (GetExtraNoiseScaleBasedOnClimate(mx - 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx - 1, my + 1) + GetExtraNoiseScaleBasedOnClimate(mx, my + 1)) / 4.0f;
            float extraNoiseScaleBasedOnClimateTopRight = (GetExtraNoiseScaleBasedOnClimate(mx, my) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my) + GetExtraNoiseScaleBasedOnClimate(mx, my + 1) + GetExtraNoiseScaleBasedOnClimate(mx + 1, my + 1)) / 4.0f;


            byte sDim = 4;
            NativeArray<float> baseHeightValueNativeArray = new NativeArray<float>(shm.Length, Allocator.TempJob);
            int i = 0;
            for (int y = 0; y < sDim; y++)
            for (int x = 0; x < sDim; x++)
                baseHeightValueNativeArray[i++] = baseHeightValue[x, y];

            i = 0;
            NativeArray<float> noiseHeightMultiplierNativeArray = new NativeArray<float>(noiseHeightMultiplierMap.Length, Allocator.TempJob);
            for (int y = 0; y < sDim; y++)
            for (int x = 0; x < sDim; x++)
                noiseHeightMultiplierNativeArray[i++] = noiseHeightMultiplierMap[x, y];

            // TODO - shortcut conversion & flattening.
            NativeArray<byte> lhmNativeArray = new NativeArray<byte>(lhm.Length, Allocator.TempJob);
            byte lDim = (byte)lhm.GetLength(0);
            i = 0;
            for (int y = 0; y < lDim; y++)
            for (int x = 0; x < lDim; x++)
                lhmNativeArray[i++] = lhm[x, y];

            // Extract height samples for all chunks
            int hDim = HeightmapDimension;
            GenerateSamplesJob generateSamplesJob = new GenerateSamplesJob
            {
                baseHeightValue = baseHeightValueNativeArray,
                lhm = lhmNativeArray,
                noiseHeightMultiplierMap = noiseHeightMultiplierNativeArray,
                heightmapData = mapPixel.heightmapData,
                sd = sDim,
                ld = lDim,
                hDim = hDim,
                div = div,
                mapPixelX = mapPixel.mapPixelX,
                mapPixelY = mapPixel.mapPixelY,
                maxTerrainHeight = MaxTerrainHeight,
                //extraNoiseScaleBasedOnClimate = extraNoiseScaleBasedOnClimate,
                extraNoiseScaleBasedOnClimateTopLeft = extraNoiseScaleBasedOnClimateTopLeft,
                extraNoiseScaleBasedOnClimateTopRight = extraNoiseScaleBasedOnClimateTopRight,
                extraNoiseScaleBasedOnClimateBottomLeft = extraNoiseScaleBasedOnClimateBottomLeft,
                extraNoiseScaleBasedOnClimateBottomRight = extraNoiseScaleBasedOnClimateBottomRight
            };

            JobHandle generateSamplesHandle = generateSamplesJob.Schedule(hDim * hDim, 64);     // Batch = 1 breaks it since shm not copied... test again later
            return generateSamplesHandle;
        }
        delegate int IDX(int r, int c, int dim);
        
    }
}